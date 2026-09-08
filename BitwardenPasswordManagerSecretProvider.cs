using System.Collections.Concurrent;
using System.Text.Json;

namespace Common.Secrets;

public sealed class BitwardenPasswordManagerSecretProvider : SecretProviderBase, ISecretProviderHealth, IDisposable
{
    private readonly BitwardenPasswordManagerOptions options;
    private readonly HttpClient httpClient;
    private readonly bool ownsHttpClient;
    private readonly SemaphoreSlim initializationLock = new(1, 1);
    private readonly ConcurrentDictionary<string, string> values = new(StringComparer.OrdinalIgnoreCase);
    private DateTimeOffset cacheExpiresAt;
    private bool initialized;

    public BitwardenPasswordManagerSecretProvider(BitwardenPasswordManagerOptions options)
        : this(options, CreateClient(options), true)
    {
    }

    public BitwardenPasswordManagerSecretProvider(BitwardenPasswordManagerOptions options, HttpClient httpClient)
        : this(options, httpClient, false)
    {
    }

    private BitwardenPasswordManagerSecretProvider(
        BitwardenPasswordManagerOptions options,
        HttpClient httpClient,
        bool ownsHttpClient)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        this.ownsHttpClient = ownsHttpClient;
    }

    public string ProviderName => "BitwardenPasswordManager";

    public bool IsEnabled => options.Enabled;

    public Exception? LastError { get; private set; }

    public override async Task<string?> GetAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (!options.Enabled)
        {
            return null;
        }

        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        return values.TryGetValue(NormalizeName(name), out string? value) ? value : null;
    }

    public async Task<SecretProviderHealth> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        if (!options.Enabled)
        {
            return new SecretProviderHealth(ProviderName, false, false, "Provider is disabled.");
        }

        try
        {
            using HttpResponseMessage response = await httpClient.GetAsync("status", cancellationToken).ConfigureAwait(false);
            bool available = response.IsSuccessStatusCode;
            LastError = null;
            return new SecretProviderHealth(
                ProviderName,
                true,
                available,
                available ? "Bitwarden Password Manager service is available." : $"HTTP {(int)response.StatusCode}.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            LastError = exception;
            return new SecretProviderHealth(ProviderName, true, false, exception.Message, exception);
        }
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (initialized && DateTimeOffset.UtcNow < cacheExpiresAt)
        {
            return;
        }

        await initializationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (initialized && DateTimeOffset.UtcNow < cacheExpiresAt)
            {
                return;
            }

            await ReloadAsync(cancellationToken).ConfigureAwait(false);
            initialized = true;
            cacheExpiresAt = DateTimeOffset.UtcNow +
                (options.CacheDuration > TimeSpan.Zero ? options.CacheDuration : TimeSpan.FromMinutes(5));
            LastError = null;
        }
        catch (Exception exception)
        {
            LastError = exception;
            initialized = false;
            throw;
        }
        finally
        {
            initializationLock.Release();
        }
    }

    private async Task ReloadAsync(CancellationToken cancellationToken)
    {
        using HttpResponseMessage status = await httpClient.GetAsync("status", cancellationToken).ConfigureAwait(false);
        status.EnsureSuccessStatusCode();

        try
        {
            using HttpResponseMessage sync = await httpClient.PostAsync("sync", null, cancellationToken).ConfigureAwait(false);
            _ = sync.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
        }

        ListResponse<ItemSummary>? list = await GetJsonAsync<ListResponse<ItemSummary>>(
            "list/object/items",
            cancellationToken).ConfigureAwait(false);

        Dictionary<string, string> refreshed = new(StringComparer.OrdinalIgnoreCase);
        if (list?.Data is not null)
        {
            foreach (ItemSummary item in list.Data)
            {
                if (string.IsNullOrWhiteSpace(item.Id) || string.IsNullOrWhiteSpace(item.Name))
                {
                    continue;
                }

                ItemDetail? detail = await GetJsonAsync<ItemDetail>(
                    $"object/item/{Uri.EscapeDataString(item.Id)}",
                    cancellationToken).ConfigureAwait(false);
                string? value = ExtractValue(detail);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    refreshed[NormalizeName(item.Name)] = value;
                }
            }
        }

        values.Clear();
        foreach ((string key, string value) in refreshed)
        {
            values[key] = value;
        }
    }

    private async Task<T?> GetJsonAsync<T>(string relativeUrl, CancellationToken cancellationToken)
        where T : class
    {
        using HttpResponseMessage response = await httpClient.GetAsync(relativeUrl, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        return await JsonSerializer.DeserializeAsync<T>(
            stream,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
            cancellationToken).ConfigureAwait(false);
    }

    private static string? ExtractValue(ItemDetail? item)
    {
        if (item is null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(item.Notes))
        {
            return item.Notes;
        }

        if (!string.IsNullOrWhiteSpace(item.Login?.Password))
        {
            return item.Login.Password;
        }

        return item.Fields?
            .FirstOrDefault(field =>
                !string.IsNullOrWhiteSpace(field.Value) &&
                (string.Equals(field.Type, "hidden", StringComparison.OrdinalIgnoreCase) || field.Type == "1"))
            ?.Value;
    }

    private static string NormalizeName(string name)
    {
        const string prefix = "Secrets:";
        string normalized = name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? name[prefix.Length..]
            : name;
        return normalized.Trim();
    }

    private static HttpClient CreateClient(BitwardenPasswordManagerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        string baseUrl = string.IsNullOrWhiteSpace(options.BaseUrl) ? "http://127.0.0.1:8087/" : options.BaseUrl.Trim();
        if (!baseUrl.EndsWith('/'))
        {
            baseUrl += "/";
        }

        return new HttpClient
        {
            BaseAddress = new Uri(baseUrl, UriKind.Absolute),
            Timeout = options.RequestTimeout > TimeSpan.Zero ? options.RequestTimeout : TimeSpan.FromSeconds(30)
        };
    }

    public void Dispose()
    {
        initializationLock.Dispose();
        if (ownsHttpClient)
        {
            httpClient.Dispose();
        }
    }

    private sealed class ListResponse<T>
    {
        public List<T> Data { get; set; } = [];
    }

    private sealed class ItemSummary
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    private sealed class ItemDetail
    {
        public string? Notes { get; set; }
        public ItemLogin? Login { get; set; }
        public List<ItemField>? Fields { get; set; }
    }

    private sealed class ItemLogin
    {
        public string? Password { get; set; }
    }

    private sealed class ItemField
    {
        public string? Value { get; set; }
        public string? Type { get; set; }
    }
}
