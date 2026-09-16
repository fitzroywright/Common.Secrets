namespace Common.Secrets;

/// <summary>
/// Provider-neutral health result for the Common.Secrets capability.
/// Consumers must not interpret provider-specific health endpoints or status codes.
/// </summary>
public sealed record CommonSecretsHealth(
    bool IsConfigured,
    bool IsAvailable,
    string ProviderName,
    string Message);

public interface ICommonSecretsHealthCheck
{
    Task<CommonSecretsHealth> CheckAsync(CancellationToken cancellationToken = default);
}

internal sealed class CommonSecretsHealthCheck : ICommonSecretsHealthCheck
{
    private readonly CommonSecretsOptions options;
    private readonly IReadOnlyDictionary<string, ISecretProviderHealth> providers;

    public CommonSecretsHealthCheck(CommonSecretsOptions options, IEnumerable<ISecretProviderHealth> providers)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.providers = providers
            .GroupBy(x => Normalize(x.ProviderName), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);
    }

    public async Task<CommonSecretsHealth> CheckAsync(CancellationToken cancellationToken = default)
    {
        string providerName = CommonSecretsPolicy.ResolveProviderOrder(options).FirstOrDefault() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(providerName))
            return new CommonSecretsHealth(false, false, "None", "No Common.Secrets provider is configured.");

        string normalized = Normalize(providerName);
        if (!providers.TryGetValue(normalized, out ISecretProviderHealth? providerHealth))
        {
            // Some providers (for example environment/configuration sources) have no remote service to probe.
            // Report that truthfully rather than inventing provider-specific behavior.
            return new CommonSecretsHealth(true, true, providerName, "Provider is configured and does not expose a remote health probe.");
        }

        SecretProviderHealth health = await providerHealth.CheckHealthAsync(cancellationToken).ConfigureAwait(false);
        return new CommonSecretsHealth(
            health.IsEnabled,
            health.IsEnabled && health.IsAvailable,
            providerName,
            health.Message ?? (health.IsAvailable ? "Secret provider is available." : "Secret provider is unavailable."));
    }

    private static string Normalize(string value)
        => string.Equals(value, "Bitwarden", StringComparison.OrdinalIgnoreCase)
            ? "BitwardenSecretsManager"
            : value.Trim();
}
