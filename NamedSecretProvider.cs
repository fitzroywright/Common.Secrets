namespace Common.Secrets;

/// <summary>
/// Gives a concrete provider its configured instance name without exposing the
/// provider implementation type as the application-facing identity.
/// </summary>
internal sealed class NamedSecretProvider : SecretProviderBase, ISecretProviderHealth
{
    private readonly string name;
    private readonly ISecretProvider inner;

    public NamedSecretProvider(string name, ISecretProvider inner)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Provider instance name is required.", nameof(name));

        this.name = name.Trim();
        this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    public string ProviderName => name;

    public bool IsEnabled =>
        inner is not ISecretProviderHealth health || health.IsEnabled;

    public Exception? LastError =>
        (inner as ISecretProviderHealth)?.LastError;

    public override Task<string?> GetAsync(
        string name,
        CancellationToken cancellationToken = default)
        => inner.GetAsync(name, cancellationToken);

    public async Task<SecretProviderHealth> CheckHealthAsync(
        CancellationToken cancellationToken = default)
    {
        if (inner is not ISecretProviderHealth health)
        {
            return new SecretProviderHealth(
                ProviderName,
                true,
                true,
                "Provider is configured and does not expose a remote health probe.");
        }

        SecretProviderHealth result =
            await health.CheckHealthAsync(cancellationToken).ConfigureAwait(false);

        return result with { ProviderName = ProviderName };
    }
}
