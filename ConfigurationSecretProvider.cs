using Microsoft.Extensions.Configuration;

namespace Common.Secrets;

public sealed class ConfigurationSecretProvider : SecretProviderBase
{
    private readonly IConfiguration configuration;

    public ConfigurationSecretProvider(IConfiguration configuration)
    {
        this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public override Task<string?> GetAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        cancellationToken.ThrowIfCancellationRequested();

        string? value = configuration[name];
        return Task.FromResult(value);
    }
}
