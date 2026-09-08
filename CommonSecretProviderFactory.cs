using Microsoft.Extensions.Configuration;

namespace Common.Secrets;

public static class CommonSecretProviderFactory
{
    public static ISecretProvider Create(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        CommonSecretsOptions commonOptions = configuration
            .GetSection("CommonSecrets")
            .Get<CommonSecretsOptions>() ?? new CommonSecretsOptions();

        OpenBaoOptions openBaoOptions = configuration
            .GetSection("CommonSecrets:OpenBao")
            .Get<OpenBaoOptions>() ?? new OpenBaoOptions();

        BitwardenSecretsManagerOptions bitwardenOptions = configuration
            .GetSection("CommonSecrets:Bitwarden")
            .Get<BitwardenSecretsManagerOptions>() ?? new BitwardenSecretsManagerOptions();

        BitwardenPasswordManagerOptions passwordManagerOptions = configuration
            .GetSection("CommonSecrets:BitwardenPasswordManager")
            .Get<BitwardenPasswordManagerOptions>() ?? new BitwardenPasswordManagerOptions();

        ISecretProvider environmentProvider = new EnvironmentSecretProvider();
        ISecretProvider passwordManagerProvider = new BitwardenPasswordManagerSecretProvider(passwordManagerOptions);
        ISecretProvider secretsManagerProvider = new BitwardenSecretProvider(bitwardenOptions);
        ISecretProvider openBaoProvider = new OpenBaoSecretProvider(openBaoOptions);
        ISecretProvider configurationProvider = new ConfigurationSecretProvider(configuration);

        Dictionary<string, ISecretProvider> providersByName = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Environment"] = environmentProvider,
            ["BitwardenPasswordManager"] = passwordManagerProvider,
            ["BitwardenSecretsManager"] = secretsManagerProvider,
            ["Bitwarden"] = secretsManagerProvider,
            ["OpenBao"] = openBaoProvider,
            ["Configuration"] = configurationProvider
        };

        string[] providerOrder = commonOptions.ProviderOrder is { Length: > 0 }
            ? commonOptions.ProviderOrder
            : new CommonSecretsOptions().ProviderOrder;

        List<ISecretProvider> orderedProviders = [];
        HashSet<ISecretProvider> includedProviders = new(ReferenceEqualityComparer.Instance);

        foreach (string configuredName in providerOrder)
        {
            string providerName = configuredName?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(providerName) ||
                !providersByName.TryGetValue(providerName, out ISecretProvider? secretProvider))
            {
                throw new InvalidOperationException(
                    $"Unknown Common.Secrets provider '{configuredName}'. Valid providers are Environment, BitwardenPasswordManager, BitwardenSecretsManager, OpenBao and Configuration. 'Bitwarden' remains supported as a legacy alias for BitwardenSecretsManager.");
            }

            if (!includedProviders.Add(secretProvider))
            {
                throw new InvalidOperationException(
                    $"Common.Secrets provider '{providerName}' appears more than once in CommonSecrets:ProviderOrder.");
            }

            orderedProviders.Add(secretProvider);
        }

        return new ChainedSecretProvider(orderedProviders);
    }
}
