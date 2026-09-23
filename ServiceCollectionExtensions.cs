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

        IReadOnlyDictionary<string, SecretProviderDefinition> definitions =
            SecretProviderConfiguration.ReadProviders(configuration);

        CommonSecretsPolicy.Validate(commonOptions, definitions);

        string[] providerOrder =
            CommonSecretsPolicy.ResolveProviderOrder(commonOptions, definitions);

        services.AddSingleton(commonOptions);
        services.TryAddSingleton<ISecretTelemetrySink, NullSecretTelemetrySink>();

        services.AddSingleton<ConfiguredSecretProviderSet>(provider =>
            CommonSecretProviderFactory.CreateProviderSet(
                configuration,
                commonOptions,
                provider.GetRequiredService<ISecretTelemetrySink>()));

        services.AddSingleton<ISecretProvider>(provider =>
            provider.GetRequiredService<ConfiguredSecretProviderSet>().Provider);

        foreach (string providerName in providerOrder)
        {
            string capturedName = providerName;
            services.AddSingleton<ISecretProviderHealth>(provider =>
                provider.GetRequiredService<ConfiguredSecretProviderSet>()
                    .HealthProviders[capturedName]);
        }

        services.AddSingleton<ICommonSecretsHealthCheck, CommonSecretsHealthCheck>();

        return services;
    }

    public static IServiceCollection AddCommonSecretsDiagnostics(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddCommonDiagnostics();
        services.Replace(ServiceDescriptor.Singleton<ISecretTelemetrySink, CommonDiagnosticsSecretTelemetrySink>());
        services.AddScoped<IDiagnosticCheck, CommonSecretsDiagnosticCheck>();
        services.AddSingleton<ILevelXLocalTest, CommonSecretsProviderRegistrationLevelXTest>();
        services.AddSingleton<ILevelXLocalTest, CommonSecretsEnabledProviderLevelXTest>();
        services.AddSingleton<ILevelXLocalTest, CommonSecretsProviderAvailabilityLevelXTest>();
        services.AddSingleton<ILevelXLocalTest, CommonSecretsIdentityCompletenessLevelXTest>();
        return services;
    }
}
