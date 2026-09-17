using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Common.Secrets.Tests;

public sealed class CommonSecretsBootstrapRuntimeTests
{
    [Fact]
    public async Task Create_UsesConfigurationProviderWithoutOpenBao()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CommonSecrets:Mode"] = "Development",
                ["CommonSecrets:ProviderOrder:0"] = "Configuration",
                ["CommonSecrets:OpenBao:Enabled"] = "false",
                ["Example:Secret"] = "configuration-value"
            })
            .Build();

        await using CommonSecretsBootstrapRuntime runtime = CommonSecretsBootstrapRuntime.Create(configuration);

        Assert.Equal("configuration-value", await runtime.Provider.GetAsync("Example:Secret"));
    }

    [Fact]
    public async Task Register_ReusesBootstrapProviderInstance()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CommonSecrets:Mode"] = "Development",
                ["CommonSecrets:ProviderOrder:0"] = "Configuration",
                ["CommonSecrets:OpenBao:Enabled"] = "false",
                ["Example:Secret"] = "shared-value"
            })
            .Build();

        await using CommonSecretsBootstrapRuntime runtime = CommonSecretsBootstrapRuntime.Create(configuration);
        ServiceCollection services = new();
        runtime.Register(services);
        await using ServiceProvider provider = services.BuildServiceProvider();

        ISecretProvider registered = provider.GetRequiredService<ISecretProvider>();
        Assert.Same(runtime.Provider, registered);
        Assert.Equal("shared-value", await registered.GetAsync("Example:Secret"));
    }
}
