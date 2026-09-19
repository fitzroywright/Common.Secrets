namespace Common.Secrets;

public enum SecretProviderAuthenticationState
{
    NotApplicable,
    Unknown,
    Authenticated,
    AuthenticationFailed
}

public sealed record CommonSecretsHealthV2(
    bool IsConfigured,
    bool IsReachable,
    SecretProviderAuthenticationState AuthenticationState,
    string ProviderName,
    IReadOnlyList<string> MissingRequiredSecrets,
    DateTimeOffset ObservedAtUtc,
    string Message)
{
    public bool IsReady =>
        IsConfigured &&
        IsReachable &&
        AuthenticationState is SecretProviderAuthenticationState.Authenticated or SecretProviderAuthenticationState.NotApplicable &&
        MissingRequiredSecrets.Count == 0;
}

public static class CommonSecretsHealthEvaluator
{
    public static async Task<CommonSecretsHealthV2> EvaluateAsync(
        ISecretProvider provider,
        ICommonSecretsHealthCheck healthCheck,
        IEnumerable<string>? requiredSecretNames = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(healthCheck);

        CommonSecretsHealth health = await healthCheck.CheckAsync(cancellationToken).ConfigureAwait(false);
        string[] required = (requiredSecretNames ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var missing = new List<string>();
        if (health.IsConfigured && health.IsAvailable)
        {
            foreach (string name in required)
            {
                try
                {
                    string? value = await provider.GetAsync(name, cancellationToken).ConfigureAwait(false);
                    if (string.IsNullOrWhiteSpace(value)) missing.Add(name);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch
                {
                    // Do not expose provider exceptions or secret values here. The provider-health
                    // result remains authoritative for reachability/authentication classification.
                    missing.Add(name);
                }
            }
        }

        SecretProviderAuthenticationState authentication =
            !health.IsConfigured
                ? SecretProviderAuthenticationState.Unknown
                : !health.IsAvailable
                    ? ClassifyAuthentication(health.Message)
                    : SecretProviderAuthenticationState.Authenticated;

        return new(
            health.IsConfigured,
            health.IsAvailable,
            authentication,
            health.ProviderName,
            missing,
            DateTimeOffset.UtcNow,
            BuildMessage(health, missing, authentication));
    }

    private static SecretProviderAuthenticationState ClassifyAuthentication(string? message)
    {
        if (!string.IsNullOrWhiteSpace(message) &&
            (message.Contains("auth", StringComparison.OrdinalIgnoreCase) ||
             message.Contains("credential", StringComparison.OrdinalIgnoreCase) ||
             message.Contains("forbidden", StringComparison.OrdinalIgnoreCase) ||
             message.Contains("unauthorized", StringComparison.OrdinalIgnoreCase)))
        {
            return SecretProviderAuthenticationState.AuthenticationFailed;
        }

        return SecretProviderAuthenticationState.Unknown;
    }

    private static string BuildMessage(
        CommonSecretsHealth health,
        IReadOnlyCollection<string> missing,
        SecretProviderAuthenticationState authentication)
    {
        if (!health.IsConfigured) return "No secret provider is configured.";
        if (!health.IsAvailable && authentication == SecretProviderAuthenticationState.AuthenticationFailed)
            return $"Secret provider {health.ProviderName} authentication failed.";
        if (!health.IsAvailable) return $"Secret provider {health.ProviderName} is unavailable.";
        if (missing.Count > 0)
            return $"Secret provider {health.ProviderName} is available but {missing.Count} required secret(s) are missing.";
        return $"Secret provider {health.ProviderName} is available and all requested required secrets are present.";
    }
}
