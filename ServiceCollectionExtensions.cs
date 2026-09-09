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

        CommonSecretsPolicy.Validate(commonOptions, openBaoOptions);

        services.AddSingleton(commonOptions);
        services.AddSingleton(openBaoOptions);
        services.AddSingleton(bitwardenOptions);
        services.TryAddSingleton<ISecretTelemetrySink, NullSecretTelemetrySink>();
        services.AddSingleton<EnvironmentSecretProvider>();
        services.AddSingleton<BitwardenSecretProvider>();
        services.AddSingleton(new ConfigurationSecretProvider(configuration));
        services.AddSingleton<IOpenBaoAuthenticator>(provider =>
        {
            HttpClient client = new();
            return new OpenBaoAuthenticator(openBaoOptions, commonOptions, client);
        });
        services.AddSingleton<OpenBaoSecretProvider>(provider =>
        {
            HttpClient client = new();
            return new OpenBaoSecretProvider(
                openBaoOptions,
                client,
                provider.GetRequiredService<IOpenBaoAuthenticator>());
        });
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

            string[] providerOrder = CommonSecretsPolicy.ResolveProviderOrder(commonOptions);
            List<ISecretProvider> orderedProviders = [];
            foreach (string providerName in providerOrder)
            {
                if (!providersByName.TryGetValue(providerName, out ISecretProvider? secretProvider))
                {
                    throw new InvalidOperationException($"Unknown Common.Secrets provider '{providerName}'.");
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
