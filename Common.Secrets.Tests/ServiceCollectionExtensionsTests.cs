using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Common.Secrets.Tests;

public sealed class ServiceCollectionExtensionsTests
{
    [Fact]
    public async Task NamedProviderResolvesThroughDependencyInjection()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CommonSecrets:Mode"] = "Development",
                ["CommonSecrets:ProviderOrder:0"] = "DeveloperVault",
                ["CommonSecrets:Providers:DeveloperVault:Type"] = "BitwardenSecretsManager",
                ["CommonSecrets:Providers:DeveloperVault:Settings:Enabled"] = "false"
            })
            .Build();

        ServiceCollection services = new();
        services.AddCommonSecrets(configuration);

        await using ServiceProvider provider =
            services.BuildServiceProvider();

        Assert.NotNull(
            provider.GetRequiredService<ISecretProvider>());

        ISecretProviderHealth health =
            Assert.Single(provider.GetServices<ISecretProviderHealth>());

        Assert.Equal("DeveloperVault", health.ProviderName);
    }

    [Fact]
    public async Task MultipleInstancesOfSameProviderTypeAreSupported()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CommonSecrets:Mode"] = "Development",
                ["CommonSecrets:ProviderOrder:0"] = "PrimaryVault",
                ["CommonSecrets:ProviderOrder:1"] = "BackupVault",

                ["CommonSecrets:Providers:PrimaryVault:Type"] =
                    "BitwardenSecretsManager",
                ["CommonSecrets:Providers:PrimaryVault:Settings:Enabled"] =
                    "false",

                ["CommonSecrets:Providers:BackupVault:Type"] =
                    "BitwardenSecretsManager",
                ["CommonSecrets:Providers:BackupVault:Settings:Enabled"] =
                    "false"
            })
            .Build();

        ServiceCollection services = new();
        services.AddCommonSecrets(configuration);

        await using ServiceProvider provider =
            services.BuildServiceProvider();

        string[] names = provider
            .GetServices<ISecretProviderHealth>()
            .Select(x => x.ProviderName)
            .ToArray();

        Assert.Equal(
            ["PrimaryVault", "BackupVault"],
            names);
    }
}
