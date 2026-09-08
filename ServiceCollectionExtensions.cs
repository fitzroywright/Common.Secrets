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

        OpenBaoOptions openBaoOptions = configuration
            .GetSection("CommonSecrets:OpenBao")
            .Get<OpenBaoOptions>() ?? new OpenBaoOptions();

        BitwardenSecretsManagerOptions bitwardenOptions = configuration
            .GetSection("CommonSecrets:Bitwarden")
            .Get<BitwardenSecretsManagerOptions>() ?? new BitwardenSecretsManagerOptions();

        services.AddSingleton(openBaoOptions);
        services.AddSingleton(bitwardenOptions);
        services.AddSingleton<EnvironmentSecretProvider>();
        services.AddSingleton<OpenBaoSecretProvider>();
        services.AddSingleton<BitwardenSecretProvider>();
        services.AddSingleton(new ConfigurationSecretProvider(configuration));
        services.AddSingleton<ISecretProvider>(provider =>
        {
            ISecretProvider[] providers =
            {
                provider.GetRequiredService<EnvironmentSecretProvider>(),
                provider.GetRequiredService<OpenBaoSecretProvider>(),
                provider.GetRequiredService<BitwardenSecretProvider>(),
                provider.GetRequiredService<ConfigurationSecretProvider>()
            };

            return new ChainedSecretProvider(providers);
        });

        return services;
    }
}
