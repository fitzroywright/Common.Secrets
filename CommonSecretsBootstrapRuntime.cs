using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Common.Secrets;

public sealed class CommonSecretsBootstrapRuntime : IAsyncDisposable
{
    private readonly HttpClient httpClient;
    private bool disposed;

    private CommonSecretsBootstrapRuntime(
        CommonSecretsOptions commonOptions,
        OpenBaoOptions openBaoOptions,
        HttpClient httpClient,
        OpenBaoAuthenticator authenticator,
        OpenBaoSecretProvider provider)
    {
        CommonOptions = commonOptions;
        OpenBaoOptions = openBaoOptions;
        this.httpClient = httpClient;
        Authenticator = authenticator;
        Provider = provider;
    }

    public CommonSecretsOptions CommonOptions { get; }

    public OpenBaoOptions OpenBaoOptions { get; }

    public OpenBaoAuthenticator Authenticator { get; }

    public OpenBaoSecretProvider Provider { get; }

    public static CommonSecretsBootstrapRuntime Create(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        CommonSecretsOptions commonOptions = configuration
            .GetSection("CommonSecrets")
            .Get<CommonSecretsOptions>() ?? new CommonSecretsOptions();
        OpenBaoOptions openBaoOptions = configuration
            .GetSection("CommonSecrets:OpenBao")
            .Get<OpenBaoOptions>() ?? new OpenBaoOptions();

        CommonSecretsPolicy.Validate(commonOptions, openBaoOptions);
        if (!openBaoOptions.Enabled)
        {
            throw new InvalidOperationException("Common.Secrets bootstrap runtime requires OpenBao to be enabled.");
        }

        HttpClient httpClient = new();
        OpenBaoAuthenticator authenticator = new(openBaoOptions, commonOptions, httpClient);
        OpenBaoSecretProvider provider = new(openBaoOptions, httpClient, authenticator);
        return new CommonSecretsBootstrapRuntime(
            commonOptions,
            openBaoOptions,
            httpClient,
            authenticator,
            provider);
    }

    public IServiceCollection Register(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton(CommonOptions);
        services.AddSingleton(OpenBaoOptions);
        services.TryAddSingleton<ISecretTelemetrySink, NullSecretTelemetrySink>();
        services.AddSingleton<IOpenBaoAuthenticator>(_ => Authenticator);
        services.AddSingleton<IOpenBaoTokenLifecycle>(_ => Authenticator);
        services.AddSingleton<OpenBaoSecretProvider>(_ => Provider);
        services.AddSingleton<ISecretProviderHealth>(_ => Provider);
        services.AddSingleton<ISecretProvider>(serviceProvider =>
            new ObservedSecretProvider(
                Provider,
                serviceProvider.GetRequiredService<ISecretTelemetrySink>(),
                CommonOptions));
        return services;
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed) return;
        disposed = true;

        Provider.Dispose();
        await Authenticator.DisposeAsync().ConfigureAwait(false);
        httpClient.Dispose();
    }
}
