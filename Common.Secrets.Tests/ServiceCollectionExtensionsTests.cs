using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Common.Secrets.Tests;

public sealed class ServiceCollectionExtensionsTests
{
    [Fact]
    public async Task DevelopmentModeResolvesCanonicalBitwardenSecretsManagerProvider()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CommonSecrets:Mode"] = "Development",
                ["CommonSecrets:ProviderOrder:0"] = "BitwardenSecretsManager",
                ["CommonSecrets:BitwardenSecretsManager:Enabled"] = "false"
            })
            .Build();

        ServiceCollection services = new();
        services.AddCommonSecrets(configuration);

        await using ServiceProvider provider = services.BuildServiceProvider();
        ISecretProvider secretProvider = provider.GetRequiredService<ISecretProvider>();

        Assert.NotNull(secretProvider);
        Assert.NotNull(provider.GetRequiredService<BitwardenSecretProvider>());
    }

    [Fact]
    public async Task DevelopmentModeResolvesPasswordManagerProvider()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CommonSecrets:Mode"] = "Development",
                ["CommonSecrets:ProviderOrder:0"] = "BitwardenPasswordManager",
                ["CommonSecrets:BitwardenPasswordManager:Enabled"] = "false"
            })
            .Build();

        ServiceCollection services = new();
        services.AddCommonSecrets(configuration);

        await using ServiceProvider provider = services.BuildServiceProvider();
        ISecretProvider secretProvider = provider.GetRequiredService<ISecretProvider>();

        Assert.NotNull(secretProvider);
        Assert.NotNull(provider.GetRequiredService<BitwardenPasswordManagerSecretProvider>());
    }

    [Fact]
    public async Task LegacyBitwardenAliasStillResolvesSecretsManager()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CommonSecrets:Mode"] = "Development",
                ["CommonSecrets:ProviderOrder:0"] = "Bitwarden",
                ["CommonSecrets:Bitwarden:Enabled"] = "false"
            })
            .Build();

        ServiceCollection services = new();
        services.AddCommonSecrets(configuration);

        await using ServiceProvider provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetRequiredService<ISecretProvider>());
    }
}
