using Microsoft.Extensions.Configuration;
using Xunit;

namespace Common.Secrets.Tests;

public sealed class CommonSecretProviderFactoryTests
{
    [Fact]
    public async Task DevelopmentCanUseNamedConfigurationProvider()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CommonSecrets:Mode"] = "Development",
                ["CommonSecrets:ProviderOrder:0"] = "LocalConfig",
                ["CommonSecrets:Providers:LocalConfig:Type"] = "Configuration",
                ["Demo:Secret"] = "configuration-value"
            })
            .Build();

        ISecretProvider provider =
            CommonSecretProviderFactory.Create(configuration);

        string? value =
            await provider.GetAsync("Demo:Secret");

        Assert.Equal("configuration-value", value);
    }

    [Fact]
    public async Task ProviderOrderReferencesInstanceNames()
    {
        const string environmentVariable =
            "COMMON_SECRETS_FACTORY_TEST_VALUE";

        string? original =
            Environment.GetEnvironmentVariable(environmentVariable);

        try
        {
            Environment.SetEnvironmentVariable(
                environmentVariable,
                "environment-value");

            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["CommonSecrets:Mode"] = "Development",
                    ["CommonSecrets:ProviderOrder:0"] = "LocalConfig",
                    ["CommonSecrets:ProviderOrder:1"] = "ProcessEnvironment",
                    ["CommonSecrets:Providers:LocalConfig:Type"] = "Configuration",
                    ["CommonSecrets:Providers:ProcessEnvironment:Type"] = "Environment",
                    [environmentVariable] = "configuration-value"
                })
                .Build();

            ISecretProvider provider =
                CommonSecretProviderFactory.Create(configuration);

            string? value =
                await provider.GetAsync(environmentVariable);

            Assert.Equal("configuration-value", value);
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                environmentVariable,
                original);
        }
    }

    [Fact]
    public void LegacyProviderSpecificShapeIsRejected()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CommonSecrets:Mode"] = "Development",
                ["CommonSecrets:ProviderOrder:0"] = "OpenBao",
                ["CommonSecrets:OpenBao:Enabled"] = "true",
                ["CommonSecrets:OpenBao:Address"] = "https://openbao.example:8200"
            })
            .Build();

        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(
                () => CommonSecretProviderFactory.Create(configuration));

        Assert.Contains(
            "CommonSecrets:Providers",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BitwardenAliasIsNotAcceptedAsAProviderType()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CommonSecrets:Mode"] = "Development",
                ["CommonSecrets:ProviderOrder:0"] = "Primary",
                ["CommonSecrets:Providers:Primary:Type"] = "Bitwarden"
            })
            .Build();

        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(
                () => CommonSecretProviderFactory.Create(configuration));

        Assert.Contains(
            "not allowed",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }
}
