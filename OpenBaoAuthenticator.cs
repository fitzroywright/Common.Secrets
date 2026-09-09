using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Common.Secrets;

public interface IOpenBaoAuthenticator
{
    Task<string> GetTokenAsync(CancellationToken cancellationToken = default);
}

public sealed class OpenBaoAuthenticator : IOpenBaoAuthenticator
{
    private readonly OpenBaoOptions options;
    private readonly CommonSecretsOptions commonOptions;
    private readonly HttpClient httpClient;
    private readonly SemaphoreSlim gate = new(1, 1);
    private string? cachedToken;
    private DateTimeOffset renewAt;

    public OpenBaoAuthenticator(
        OpenBaoOptions options,
        CommonSecretsOptions commonOptions,
        HttpClient httpClient)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.commonOptions = commonOptions ?? throw new ArgumentNullException(nameof(commonOptions));
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public async Task<string> GetTokenAsync(CancellationToken cancellationToken = default)
    {
        if (commonOptions.Mode != SecretsEnvironmentMode.Production)
        {
            string? developmentToken = Environment.GetEnvironmentVariable(
                options.DevelopmentTokenEnvironmentVariable);
            developmentToken = string.IsNullOrWhiteSpace(developmentToken)
                ? options.DevelopmentToken
                : developmentToken;

            if (!string.IsNullOrWhiteSpace(developmentToken))
            {
                return developmentToken.Trim();
            }
        }

        if (!string.IsNullOrWhiteSpace(cachedToken) && DateTimeOffset.UtcNow < renewAt)
        {
            return cachedToken;
        }

        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!string.IsNullOrWhiteSpace(cachedToken) && DateTimeOffset.UtcNow < renewAt)
            {
                return cachedToken;
            }

            string roleId = options.RoleId?.Trim() ?? string.Empty;
            string secretId = Environment.GetEnvironmentVariable(options.SecretIdEnvironmentVariable)?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(roleId) || string.IsNullOrWhiteSpace(secretId))
            {
                throw new InvalidOperationException(
                    "OpenBao workload authentication requires RoleId and a protected SecretId environment value.");
            }

            Uri address = GetValidatedAddress();
            string authMount = NormalizePath(options.AuthMountPath, "approle");
            Uri loginUri = new(address, $"v1/auth/{authMount}/login");
            string json = JsonSerializer.Serialize(new Dictionary<string, string>
            {
                ["role_id"] = roleId,
                ["secret_id"] = secretId
            });
            using HttpRequestMessage request = new(HttpMethod.Post, loginUri)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using HttpResponseMessage response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using JsonDocument document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            JsonElement auth = document.RootElement.GetProperty("auth");
            string token = auth.GetProperty("client_token").GetString()
                ?? throw new InvalidOperationException("OpenBao authentication response did not contain a client token.");
            int leaseSeconds = auth.TryGetProperty("lease_duration", out JsonElement lease) && lease.TryGetInt32(out int seconds)
                ? seconds
                : 300;

            cachedToken = token;
            renewAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(30, leaseSeconds / 2));
            return token;
        }
        finally
        {
            gate.Release();
        }
    }

    private Uri GetValidatedAddress()
    {
        if (!Uri.TryCreate(options.Address, UriKind.Absolute, out Uri? address))
        {
            throw new InvalidOperationException("CommonSecrets:OpenBao:Address must be a valid absolute URI.");
        }

        if (options.RequireHttps && address.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException("OpenBao authentication requires HTTPS in this environment.");
        }

        string normalized = address.AbsoluteUri.EndsWith("/", StringComparison.Ordinal)
            ? address.AbsoluteUri
            : address.AbsoluteUri + "/";
        return new Uri(normalized, UriKind.Absolute);
    }

    private static string NormalizePath(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        return string.Join(
            '/',
            value.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(Uri.EscapeDataString));
    }
}
