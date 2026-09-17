using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Common.Secrets;

/// <summary>
/// Builds the standard Common.Secrets provider graph early enough for bootstrap-time
/// secret resolution, then allows the same resolved graph to be registered into the
/// application's service collection. Bootstrap and runtime therefore use the same
/// provider policy and instances.
/// </summary>
public sealed class CommonSecretsBootstrapRuntime : IAsyncDisposable
{
    private readonly ServiceProvider bootstrapServices;
    private bool disposed;

    private CommonSecretsBootstrapRuntime(ServiceProvider bootstrapServices)
    {
        this.bootstrapServices = bootstrapServices;
        Provider = bootstrapServices.GetRequiredService<ISecretProvider>();
        HealthCheck = bootstrapServices.GetRequiredService<ICommonSecretsHealthCheck>();
        CommonOptions = bootstrapServices.GetRequiredService<CommonSecretsOptions>();
        OpenBaoOptions = bootstrapServices.GetRequiredService<OpenBaoOptions>();
        BitwardenPasswordManagerOptions = bootstrapServices.GetRequiredService<BitwardenPasswordManagerOptions>();
        BitwardenSecretsManagerOptions = bootstrapServices.GetRequiredService<BitwardenSecretsManagerOptions>();
        ProviderHealth = bootstrapServices.GetServices<ISecretProviderHealth>().ToArray();
    }

    public CommonSecretsOptions CommonOptions { get; }

    public OpenBaoOptions OpenBaoOptions { get; }

    public BitwardenPasswordManagerOptions BitwardenPasswordManagerOptions { get; }

    public BitwardenSecretsManagerOptions BitwardenSecretsManagerOptions { get; }

    public ISecretProvider Provider { get; }

    public ICommonSecretsHealthCheck HealthCheck { get; }

    public IReadOnlyList<ISecretProviderHealth> ProviderHealth { get; }

    public static CommonSecretsBootstrapRuntime Create(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        ServiceCollection services = new();
        services.AddCommonSecrets(configuration);
        ServiceProvider provider = services.BuildServiceProvider();
        return new CommonSecretsBootstrapRuntime(provider);
    }

    public IServiceCollection Register(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton(CommonOptions);
        services.AddSingleton(OpenBaoOptions);
        services.AddSingleton(BitwardenPasswordManagerOptions);
        services.AddSingleton(BitwardenSecretsManagerOptions);
        services.AddSingleton(Provider);
        services.AddSingleton<ISecretProvider>(_ => Provider);
        services.AddSingleton<ICommonSecretsHealthCheck>(_ => HealthCheck);

        foreach (ISecretProviderHealth providerHealth in ProviderHealth)
        {
            services.AddSingleton(typeof(ISecretProviderHealth), providerHealth);
        }

        return services;
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed) return;
        disposed = true;
        await bootstrapServices.DisposeAsync().ConfigureAwait(false);
    }
}
