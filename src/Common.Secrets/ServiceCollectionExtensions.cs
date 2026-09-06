using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Common.Secrets;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCommonSecrets(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSingleton<EnvironmentSecretProvider>();
        services.AddSingleton(new ConfigurationSecretProvider(configuration));
        services.AddSingleton<ISecretProvider>(provider =>
        {
            ISecretProvider[] providers =
            {
                provider.GetRequiredService<EnvironmentSecretProvider>(),
                provider.GetRequiredService<ConfigurationSecretProvider>()
            };

            return new ChainedSecretProvider(providers);
        });

        return services;
    }
}
