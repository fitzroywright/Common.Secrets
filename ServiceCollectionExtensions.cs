using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Common.Secrets;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCommonSecrets(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
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

        services.AddSingleton(commonOptions);
        services.AddSingleton(openBaoOptions);
        services.AddSingleton(bitwardenOptions);
        services.TryAddSingleton<ISecretTelemetrySink, NullSecretTelemetrySink>();
        services.AddSingleton<EnvironmentSecretProvider>();
        services.AddSingleton<OpenBaoSecretProvider>();
        services.AddSingleton<BitwardenSecretProvider>();
        services.AddSingleton(new ConfigurationSecretProvider(configuration));
        services.AddSingleton<ISecretProvider>(provider =>
        {
            Dictionary<string, ISecretProvider> providersByName = new(StringComparer.OrdinalIgnoreCase)
            {
                ["Environment"] = provider.GetRequiredService<EnvironmentSecretProvider>(),
                ["OpenBao"] = provider.GetRequiredService<OpenBaoSecretProvider>(),
                ["Bitwarden"] = provider.GetRequiredService<BitwardenSecretProvider>(),
                ["Configuration"] = provider.GetRequiredService<ConfigurationSecretProvider>()
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
                        $"Unknown Common.Secrets provider '{providerName}'. Valid providers are Environment, OpenBao, Bitwarden and Configuration.");
                }

                orderedProviders.Add(secretProvider);
            }

            ChainedSecretProvider chain = new(orderedProviders);
            return new ObservedSecretProvider(
                chain,
                provider.GetRequiredService<ISecretTelemetrySink>(),
                commonOptions);
        });

        return services;
    }
}
