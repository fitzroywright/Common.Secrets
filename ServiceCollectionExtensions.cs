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
            ISecretTelemetrySink telemetry = provider.GetRequiredService<ISecretTelemetrySink>();
            Dictionary<string, ISecretProvider> providersByName = new(StringComparer.OrdinalIgnoreCase)
            {
                ["Environment"] = Observe(provider.GetRequiredService<EnvironmentSecretProvider>(), telemetry, commonOptions),
                ["OpenBao"] = Observe(provider.GetRequiredService<OpenBaoSecretProvider>(), telemetry, commonOptions),
                ["Bitwarden"] = Observe(provider.GetRequiredService<BitwardenSecretProvider>(), telemetry, commonOptions),
                ["Configuration"] = Observe(provider.GetRequiredService<ConfigurationSecretProvider>(), telemetry, commonOptions)
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

            return new ChainedSecretProvider(orderedProviders);
        });

        return services;
    }

    private static ISecretProvider Observe(
        ISecretProvider provider,
        ISecretTelemetrySink telemetry,
        CommonSecretsOptions options)
    {
        return new ObservedSecretProvider(provider, telemetry, options);
    }
}
