namespace Common.Secrets;

public sealed class ChainedSecretProvider : SecretProviderBase
{
    private readonly IReadOnlyList<ISecretProvider> providers;

    public ChainedSecretProvider(IEnumerable<ISecretProvider> providers)
    {
        this.providers = (providers ?? throw new ArgumentNullException(nameof(providers))).ToArray();
    }

    public override async Task<string?> GetAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        foreach (ISecretProvider provider in providers)
        {
            string? value = await provider.GetAsync(name, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }
}
