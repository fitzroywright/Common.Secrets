using Microsoft.Extensions.Configuration;
using Xunit;

namespace Common.Secrets.Tests;

public sealed class CommonSecretProviderFactoryTests
{
    [Fact]
    public void DefaultModeIsProduction()
    {
        CommonSecretsOptions options = new();

        Assert.Equal(SecretsEnvironmentMode.Production, options.Mode);
        Assert.Equal(["OpenBao"], CommonSecretsPolicy.ResolveProviderOrder(options));
    }

    [Fact]
    public async Task DevelopmentCanUseConfigurationOnly()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CommonSecrets:Mode"] = "Development",
                ["CommonSecrets:ProviderOrder:0"] = "Configuration",
                ["Demo:Secret"] = "configuration-value"
            })
            .Build();

        ISecretProvider provider = CommonSecretProviderFactory.Create(configuration);

        string? value = await provider.GetAsync("Demo:Secret");

        Assert.Equal("configuration-value", value);
    }

    [Fact]
    public async Task DevelopmentConfiguredOrderControlsPrecedence()
    {
        const string environmentVariable = "COMMON_SECRETS_FACTORY_TEST_VALUE";
        string? original = Environment.GetEnvironmentVariable(environmentVariable);

        try
        {
            Environment.SetEnvironmentVariable(environmentVariable, "environment-value");

            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["CommonSecrets:Mode"] = "Development",
                    ["CommonSecrets:ProviderOrder:0"] = "Configuration",
                    ["CommonSecrets:ProviderOrder:1"] = "Environment",
                    [environmentVariable] = "configuration-value"
                })
                .Build();

            ISecretProvider provider = CommonSecretProviderFactory.Create(configuration);
            string? value = await provider.GetAsync(environmentVariable);

            Assert.Equal("configuration-value", value);
        }
        finally
        {
            Environment.SetEnvironmentVariable(environmentVariable, original);
        }
    }

    [Fact]
    public void DevelopmentRejectsDuplicateBitwardenAlias()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CommonSecrets:Mode"] = "Development",
                ["CommonSecrets:ProviderOrder:0"] = "Bitwarden",
                ["CommonSecrets:ProviderOrder:1"] = "BitwardenSecretsManager"
            })
            .Build();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => CommonSecretProviderFactory.Create(configuration));

        Assert.Contains("appears more than once", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
