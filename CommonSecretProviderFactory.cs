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

        Dictionary<string, ISecretProvider> providersByName = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Environment"] = new EnvironmentSecretProvider(),
            ["OpenBao"] = new OpenBaoSecretProvider(openBaoOptions),
            ["Bitwarden"] = new BitwardenSecretProvider(bitwardenOptions),
            ["BitwardenPasswordManager"] = new BitwardenPasswordManagerSecretProvider(passwordManagerOptions),
            ["Configuration"] = new ConfigurationSecretProvider(configuration)
        };

        string[] providerOrder = commonOptions.ProviderOrder is { Length: > 0 }
            ? commonOptions.ProviderOrder
            : new CommonSecretsOptions().ProviderOrder;

        List<ISecretProvider> orderedProviders = [];
        foreach (string providerName in providerOrder)
        {
            if (!providersByName.TryGetValue(providerName, out ISecretProvider? secretProvider))
            {
                throw new InvalidOperationException(
                    $"Unknown Common.Secrets provider '{providerName}'. Valid providers are Environment, OpenBao, Bitwarden, BitwardenPasswordManager and Configuration.");
            }

            orderedProviders.Add(secretProvider);
        }

        return new ChainedSecretProvider(orderedProviders);
    }
}
