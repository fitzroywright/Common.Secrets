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
    private readonly ConfiguredSecretProviderSet providerSet;

    public CommonSecretsHealthCheck(ConfiguredSecretProviderSet providerSet)
    {
        this.providerSet =
            providerSet ?? throw new ArgumentNullException(nameof(providerSet));
    }

    public async Task<CommonSecretsHealth> CheckAsync(
        CancellationToken cancellationToken = default)
    {
        string providerName =
            providerSet.Order.FirstOrDefault() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(providerName))
        {
            return new CommonSecretsHealth(
                false,
                false,
                "None",
                "No Common.Secrets provider is configured.");
        }

        if (!providerSet.HealthProviders.TryGetValue(
                providerName,
                out ISecretProviderHealth? providerHealth))
        {
            return new CommonSecretsHealth(
                true,
                true,
                providerName,
                "Provider is configured and does not expose a remote health probe.");
        }

        SecretProviderHealth health =
            await providerHealth.CheckHealthAsync(cancellationToken)
                .ConfigureAwait(false);

        return new CommonSecretsHealth(
            health.IsEnabled,
            health.IsEnabled && health.IsAvailable,
            providerName,
            health.Message ??
                (health.IsAvailable
                    ? "Secret provider is available."
                    : "Secret provider is unavailable."));
    }
}
