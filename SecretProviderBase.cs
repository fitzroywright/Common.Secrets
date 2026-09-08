namespace Common.Secrets;

public abstract class SecretProviderBase : ISecretProvider
{
    public abstract Task<string?> GetAsync(
        string name,
        CancellationToken cancellationToken = default);

    public async Task<string> GetRequiredAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        string? value = await GetAsync(name, cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Required secret '{name}' was not found.");
        }

        return value;
    }
}
