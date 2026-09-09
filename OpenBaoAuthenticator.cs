using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Common.Secrets;

public interface IOpenBaoAuthenticator
{
    Task<string> GetTokenAsync(CancellationToken cancellationToken = default);
}

public interface IOpenBaoTokenLifecycle
{
    Task RevokeAsync(CancellationToken cancellationToken = default);
}

public sealed class OpenBaoAuthenticator : IOpenBaoAuthenticator, IOpenBaoTokenLifecycle, IAsyncDisposable
{
    private readonly OpenBaoOptions options;
    private readonly CommonSecretsOptions commonOptions;
    private readonly HttpClient httpClient;
    private readonly SemaphoreSlim gate = new(1, 1);
    private string? cachedToken;
    private DateTimeOffset expiresAt;
    private DateTimeOffset renewAt;
    private bool renewable;

    public OpenBaoAuthenticator(OpenBaoOptions options, CommonSecretsOptions commonOptions, HttpClient httpClient)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.commonOptions = commonOptions ?? throw new ArgumentNullException(nameof(commonOptions));
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public async Task<string> GetTokenAsync(CancellationToken cancellationToken = default)
    {
        if (commonOptions.Mode != SecretsEnvironmentMode.Production)
        {
            string? developmentToken = Environment.GetEnvironmentVariable(options.DevelopmentTokenEnvironmentVariable);
            developmentToken = string.IsNullOrWhiteSpace(developmentToken) ? options.DevelopmentToken : developmentToken;
            if (!string.IsNullOrWhiteSpace(developmentToken)) return developmentToken.Trim();
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        if (!string.IsNullOrWhiteSpace(cachedToken) && now < renewAt) return cachedToken;

        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            now = DateTimeOffset.UtcNow;
            if (!string.IsNullOrWhiteSpace(cachedToken) && now < renewAt) return cachedToken;

            if (!string.IsNullOrWhiteSpace(cachedToken) && now < expiresAt && renewable && options.RenewTokens)
            {
                if (await TryRenewAsync(cachedToken, cancellationToken).ConfigureAwait(false)) return cachedToken;
                ClearToken();
            }
            else if (now >= expiresAt)
            {
                ClearToken();
            }

            return await LoginAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task RevokeAsync(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (string.IsNullOrWhiteSpace(cachedToken)) return;
            string token = cachedToken;
            ClearToken();
            using HttpRequestMessage request = new(HttpMethod.Post, new Uri(GetValidatedAddress(), "v1/auth/token/revoke-self"));
            request.Headers.Add("X-Vault-Token", token);
            using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
        }
        finally
        {
            gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (options.RevokeTokenOnDispose && commonOptions.Mode == SecretsEnvironmentMode.Production)
        {
            try { await RevokeAsync().ConfigureAwait(false); } catch { ClearToken(); }
        }
        gate.Dispose();
    }

    private async Task<string> LoginAsync(CancellationToken cancellationToken)
    {
        string roleId = options.RoleId?.Trim() ?? string.Empty;
        string secretId = await ReadBootstrapSecretIdAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(roleId) || string.IsNullOrWhiteSpace(secretId))
        {
            throw new InvalidOperationException("OpenBao workload authentication requires RoleId and a protected SecretId bootstrap value.");
        }

        string authMount = NormalizePath(options.AuthMountPath, "approle");
        Uri loginUri = new(GetValidatedAddress(), $"v1/auth/{authMount}/login");
        string json = JsonSerializer.Serialize(new Dictionary<string, string> { ["role_id"] = roleId, ["secret_id"] = secretId });
        using HttpRequestMessage request = new(HttpMethod.Post, loginUri) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        using HttpResponseMessage response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using JsonDocument document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        ApplyLease(document.RootElement.GetProperty("auth"));
        return cachedToken!;
    }

    private async Task<string> ReadBootstrapSecretIdAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(options.SecretIdFile))
        {
            string path = Path.GetFullPath(options.SecretIdFile);
            if (!File.Exists(path))
            {
                throw new InvalidOperationException("Configured OpenBao SecretId bootstrap file does not exist.");
            }

            string secretId = (await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false)).Trim();
            if (options.DeleteSecretIdFileAfterRead)
            {
                File.Delete(path);
            }
            return secretId;
        }

        if (commonOptions.Mode == SecretsEnvironmentMode.Production && options.RequireSecretIdFileInProduction)
        {
            throw new InvalidOperationException("Production OpenBao bootstrap requires a protected one-time SecretId file.");
        }

        string secretIdFromEnvironment = Environment.GetEnvironmentVariable(options.SecretIdEnvironmentVariable)?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(secretIdFromEnvironment) && options.ClearSecretIdEnvironmentVariableAfterRead)
        {
            Environment.SetEnvironmentVariable(options.SecretIdEnvironmentVariable, null);
        }
        return secretIdFromEnvironment;
    }

    private async Task<bool> TryRenewAsync(string token, CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri(GetValidatedAddress(), "v1/auth/token/renew-self"));
        request.Headers.Add("X-Vault-Token", token);
        using HttpResponseMessage response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) return false;
        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using JsonDocument document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        ApplyLease(document.RootElement.GetProperty("auth"));
        return true;
    }

    private void ApplyLease(JsonElement auth)
    {
        cachedToken = auth.GetProperty("client_token").GetString() ?? cachedToken
            ?? throw new InvalidOperationException("OpenBao authentication response did not contain a client token.");
        int leaseSeconds = auth.TryGetProperty("lease_duration", out JsonElement lease) && lease.TryGetInt32(out int seconds) ? seconds : 300;
        renewable = auth.TryGetProperty("renewable", out JsonElement renewableElement) && renewableElement.ValueKind == JsonValueKind.True;
        DateTimeOffset now = DateTimeOffset.UtcNow;
        expiresAt = now.AddSeconds(Math.Max(1, leaseSeconds));
        TimeSpan safety = options.TokenRenewalSafetyWindow < TimeSpan.Zero ? TimeSpan.Zero : options.TokenRenewalSafetyWindow;
        TimeSpan halfLease = TimeSpan.FromSeconds(Math.Max(1, leaseSeconds / 2.0));
        renewAt = now + (halfLease < safety ? halfLease : expiresAt - now - safety);
        if (renewAt <= now) renewAt = now.AddSeconds(1);
    }

    private void ClearToken()
    {
        cachedToken = null;
        expiresAt = default;
        renewAt = default;
        renewable = false;
    }

    private Uri GetValidatedAddress()
    {
        if (!Uri.TryCreate(options.Address, UriKind.Absolute, out Uri? address)) throw new InvalidOperationException("CommonSecrets:OpenBao:Address must be a valid absolute URI.");
        if (options.RequireHttps && address.Scheme != Uri.UriSchemeHttps) throw new InvalidOperationException("OpenBao authentication requires HTTPS in this environment.");
        return new Uri(address.AbsoluteUri.EndsWith("/", StringComparison.Ordinal) ? address.AbsoluteUri : address.AbsoluteUri + "/", UriKind.Absolute);
    }

    private static string NormalizePath(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;
        return string.Join('/', value.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(Uri.EscapeDataString));
    }
}
