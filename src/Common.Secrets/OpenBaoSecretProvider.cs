using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Common.Secrets;

public sealed class OpenBaoSecretProvider : SecretProviderBase, IDisposable
{
    private readonly OpenBaoOptions options;
    private readonly HttpClient httpClient;
    private readonly bool ownsHttpClient;

    public OpenBaoSecretProvider(OpenBaoOptions options)
        : this(options, new HttpClient(), true)
    {
    }

    internal OpenBaoSecretProvider(OpenBaoOptions options, HttpClient httpClient)
        : this(options, httpClient, false)
    {
    }

    private OpenBaoSecretProvider(OpenBaoOptions options, HttpClient httpClient, bool ownsHttpClient)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        this.ownsHttpClient = ownsHttpClient;
    }

    public override async Task<string?> GetAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (!options.Enabled)
        {
            return null;
        }

        Uri address = GetValidatedAddress();
        string token = GetToken();
        string requestPath = BuildRequestPath(name);
        using HttpRequestMessage request = new(HttpMethod.Get, new Uri(address, requestPath));
        request.Headers.Add("X-Vault-Token", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using HttpResponseMessage response = await httpClient
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        await using Stream content = await response.Content
            .ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        using JsonDocument document = await JsonDocument
            .ParseAsync(content, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (!document.RootElement.TryGetProperty("data", out JsonElement outerData) ||
            !outerData.TryGetProperty("data", out JsonElement secretData) ||
            !secretData.TryGetProperty("value", out JsonElement valueElement))
        {
            return null;
        }

        return valueElement.ValueKind == JsonValueKind.String
            ? valueElement.GetString()
            : valueElement.GetRawText();
    }

    public void Dispose()
    {
        if (ownsHttpClient)
        {
            httpClient.Dispose();
        }
    }

    private Uri GetValidatedAddress()
    {
        if (!Uri.TryCreate(options.Address, UriKind.Absolute, out Uri? address))
        {
            throw new InvalidOperationException("CommonSecrets:OpenBao:Address must be a valid absolute URI when OpenBao is enabled.");
        }

        if (options.RequireHttps && !string.Equals(address.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("OpenBao requires HTTPS. Set CommonSecrets:OpenBao:RequireHttps=false only for an explicitly trusted development network.");
        }

        string normalized = address.AbsoluteUri.EndsWith('/', StringComparison.Ordinal)
            ? address.AbsoluteUri
            : address.AbsoluteUri + "/";
        return new Uri(normalized, UriKind.Absolute);
    }

    private string GetToken()
    {
        string environmentVariable = string.IsNullOrWhiteSpace(options.TokenEnvironmentVariable)
            ? "OPENBAO_TOKEN"
            : options.TokenEnvironmentVariable.Trim();
        string? token = Environment.GetEnvironmentVariable(environmentVariable);
        token = string.IsNullOrWhiteSpace(token) ? options.Token : token;

        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException(
                $"OpenBao is enabled but no token was provided. Set environment variable '{environmentVariable}' or CommonSecrets:OpenBao:Token.");
        }

        return token.Trim();
    }

    private string BuildRequestPath(string name)
    {
        string mountPath = NormalizePathSegment(options.MountPath, "secret");
        string basePath = NormalizePath(options.BasePath);
        string secretPath = NormalizePath(name.Replace(':', '/'));

        string combined = string.IsNullOrWhiteSpace(basePath)
            ? secretPath
            : $"{basePath}/{secretPath}";

        return $"v1/{mountPath}/data/{combined}";
    }

    private static string NormalizePathSegment(string? value, string fallback)
    {
        string normalized = NormalizePath(value);
        return string.IsNullOrWhiteSpace(normalized) ? fallback : normalized;
    }

    private static string NormalizePath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return string.Join(
            '/',
            value
                .Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(Uri.EscapeDataString));
    }
}
