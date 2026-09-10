using Common.Diagnostics;
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
        BitwardenPasswordManagerOptions bitwardenPasswordManagerOptions = configuration
            .GetSection("CommonSecrets:BitwardenPasswordManager")
            .Get<BitwardenPasswordManagerOptions>() ?? new BitwardenPasswordManagerOptions();
        BitwardenSecretsManagerOptions bitwardenSecretsManagerOptions = configuration
            .GetSection("CommonSecrets:BitwardenSecretsManager")
            .Get<BitwardenSecretsManagerOptions>()
            ?? configuration.GetSection("CommonSecrets:Bitwarden").Get<BitwardenSecretsManagerOptions>()
            ?? new BitwardenSecretsManagerOptions();

        CommonSecretsPolicy.Validate(commonOptions, openBaoOptions);

        services.AddSingleton(commonOptions);
        services.AddSingleton(openBaoOptions);
        services.AddSingleton(bitwardenPasswordManagerOptions);
        services.AddSingleton(bitwardenSecretsManagerOptions);
        services.TryAddSingleton<ISecretTelemetrySink, NullSecretTelemetrySink>();
        services.AddSingleton<EnvironmentSecretProvider>();
        services.AddSingleton<BitwardenPasswordManagerSecretProvider>();
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

        services.AddSingleton<ISecretProviderHealth>(provider => provider.GetRequiredService<OpenBaoSecretProvider>());
        services.AddSingleton<ISecretProviderHealth>(provider => provider.GetRequiredService<BitwardenPasswordManagerSecretProvider>());
        services.AddSingleton<ISecretProviderHealth>(provider => provider.GetRequiredService<BitwardenSecretProvider>());

        services.AddSingleton<ISecretProvider>(provider =>
        {
            ISecretTelemetrySink telemetry = provider.GetRequiredService<ISecretTelemetrySink>();
            ISecretProvider bitwardenSecretsManager = Observe(
                provider.GetRequiredService<BitwardenSecretProvider>(),
                telemetry,
                commonOptions);

            Dictionary<string, ISecretProvider> providersByName = new(StringComparer.OrdinalIgnoreCase)
            {
                ["Environment"] = Observe(provider.GetRequiredService<EnvironmentSecretProvider>(), telemetry, commonOptions),
                ["OpenBao"] = Observe(provider.GetRequiredService<OpenBaoSecretProvider>(), telemetry, commonOptions),
                ["BitwardenPasswordManager"] = Observe(provider.GetRequiredService<BitwardenPasswordManagerSecretProvider>(), telemetry, commonOptions),
                ["BitwardenSecretsManager"] = bitwardenSecretsManager,
                ["Bitwarden"] = bitwardenSecretsManager,
                ["Configuration"] = Observe(provider.GetRequiredService<ConfigurationSecretProvider>(), telemetry, commonOptions)
            };

            string[] providerOrder = CommonSecretsPolicy.ResolveProviderOrder(commonOptions);
            List<ISecretProvider> orderedProviders = [];
            foreach (string configuredProviderName in providerOrder)
            {
                string providerName = NormalizeProviderName(configuredProviderName);
                if (!providersByName.TryGetValue(providerName, out ISecretProvider? secretProvider))
                {
                    throw new InvalidOperationException($"Unknown Common.Secrets provider '{configuredProviderName}'.");
                }

                orderedProviders.Add(secretProvider);
            }

            return new ChainedSecretProvider(orderedProviders);
        });

        return services;
    }

    public static IServiceCollection AddCommonSecretsDiagnostics(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddCommonDiagnostics();
        services.Replace(ServiceDescriptor.Singleton<ISecretTelemetrySink, CommonDiagnosticsSecretTelemetrySink>());
        services.AddScoped<IDiagnosticCheck, CommonSecretsDiagnosticCheck>();
        return services;
    }

    private static ISecretProvider Observe(
        ISecretProvider provider,
        ISecretTelemetrySink telemetry,
        CommonSecretsOptions options)
    {
        return new ObservedSecretProvider(provider, telemetry, options);
    }

    private static string NormalizeProviderName(string providerName)
    {
        return string.Equals(providerName, "Bitwarden", StringComparison.OrdinalIgnoreCase)
            ? "BitwardenSecretsManager"
            : providerName;
    }
}
