using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Common.Secrets;

public sealed class OpenBaoSecretProvider : SecretProviderBase, ISecretProviderHealth, IDisposable
{
    private readonly OpenBaoOptions options;
    private readonly HttpClient httpClient;
    private readonly bool ownsHttpClient;
    private readonly ConcurrentDictionary<string, string> memoryCache = new(StringComparer.OrdinalIgnoreCase);
    private Exception? lastError;

    public OpenBaoSecretProvider(OpenBaoOptions options)
        : this(options, new HttpClient(), true)
    {
    }

    public OpenBaoSecretProvider(OpenBaoOptions options, HttpClient httpClient)
        : this(options, httpClient, false)
    {
    }

    private OpenBaoSecretProvider(OpenBaoOptions options, HttpClient httpClient, bool ownsHttpClient)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        this.ownsHttpClient = ownsHttpClient;
    }

    public string ProviderName => "OpenBao";

    public bool IsEnabled => options.Enabled;

    public Exception? LastError => lastError;

    public override async Task<string?> GetAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (!options.Enabled)
        {
            return null;
        }

        if (memoryCache.TryGetValue(name, out string? cached))
        {
            return cached;
        }

        try
        {
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
                lastError = null;
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
                lastError = null;
                return null;
            }

            string? value = valueElement.ValueKind == JsonValueKind.String
                ? valueElement.GetString()
                : valueElement.GetRawText();

            if (!string.IsNullOrEmpty(value))
            {
                memoryCache.TryAdd(name, value);
            }

            lastError = null;
            return value;
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            lastError = exception;
            throw;
        }
    }

    public async Task<SecretProviderHealth> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        if (!options.Enabled)
        {
            return new SecretProviderHealth(ProviderName, false, false, "Provider is disabled.");
        }

        try
        {
            Uri address = GetValidatedAddress();
            string token = GetToken();
            using HttpRequestMessage request = new(HttpMethod.Get, new Uri(address, "v1/sys/health"));
            request.Headers.Add("X-Vault-Token", token);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using HttpResponseMessage response = await httpClient
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            bool available = response.IsSuccessStatusCode ||
                response.StatusCode == HttpStatusCode.TooManyRequests ||
                response.StatusCode == HttpStatusCode.ServiceUnavailable;

            if (!available)
            {
                response.EnsureSuccessStatusCode();
            }

            lastError = null;
            return new SecretProviderHealth(
                ProviderName,
                true,
                available,
                available ? $"OpenBao responded with HTTP {(int)response.StatusCode}." : "OpenBao is unavailable.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            lastError = exception;
            return new SecretProviderHealth(ProviderName, true, false, exception.Message, exception);
        }
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

        string normalized = address.AbsoluteUri.EndsWith("/", StringComparison.Ordinal)
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
