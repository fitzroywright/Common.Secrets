namespace Common.Secrets;

public sealed record SecretProviderHealth(
    string ProviderName,
    bool IsEnabled,
    bool IsAvailable,
    string? Message = null,
    Exception? Exception = null);

public interface ISecretProviderHealth
{
    string ProviderName { get; }

    bool IsEnabled { get; }

    Exception? LastError { get; }

    Task<SecretProviderHealth> CheckHealthAsync(
        CancellationToken cancellationToken = default);
}
