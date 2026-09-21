using Microsoft.Extensions.Configuration;
using Xunit;

namespace Common.Secrets.Tests;

public sealed class CommonSecretsPolicyTests
{
    [Fact]
    public void SingleProviderMayOmitProviderOrder()
    {
        IConfiguration configuration = Build(new Dictionary<string, string?>
        {
            ["CommonSecrets:Mode"] = "Development",
            ["CommonSecrets:Providers:OnlyProvider:Type"] = "Configuration"
        });

        CommonSecretsOptions options =
            configuration.GetSection("CommonSecrets")
                .Get<CommonSecretsOptions>()!;

        IReadOnlyDictionary<string, SecretProviderDefinition> providers =
            SecretProviderConfiguration.ReadProviders(configuration);

        Assert.Equal(
            ["OnlyProvider"],
            CommonSecretsPolicy.ResolveProviderOrder(options, providers));
    }

    [Fact]
    public void MultipleProvidersRequireExplicitOrder()
    {
        IConfiguration configuration = Build(new Dictionary<string, string?>
        {
            ["CommonSecrets:Mode"] = "Development",
            ["CommonSecrets:Providers:One:Type"] = "Configuration",
            ["CommonSecrets:Providers:Two:Type"] = "Environment"
        });

        CommonSecretsOptions options =
            configuration.GetSection("CommonSecrets")
                .Get<CommonSecretsOptions>()!;

        IReadOnlyDictionary<string, SecretProviderDefinition> providers =
            SecretProviderConfiguration.ReadProviders(configuration);

        Assert.Throws<InvalidOperationException>(
            () => CommonSecretsPolicy.ResolveProviderOrder(options, providers));
    }

    [Fact]
    public void ProductionRejectsNonOpenBaoProviderType()
    {
        IConfiguration configuration = Build(new Dictionary<string, string?>
        {
            ["CommonSecrets:Mode"] = "Production",
            ["CommonSecrets:ProviderOrder:0"] = "Fallback",
            ["CommonSecrets:Providers:Fallback:Type"] = "Configuration"
        });

        CommonSecretsOptions options =
            configuration.GetSection("CommonSecrets")
                .Get<CommonSecretsOptions>()!;

        IReadOnlyDictionary<string, SecretProviderDefinition> providers =
            SecretProviderConfiguration.ReadProviders(configuration);

        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(
                () => CommonSecretsPolicy.Validate(options, providers));

        Assert.Contains(
            "not allowed",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ProductionOpenBaoRequiresEnabledHttpsSettings()
    {
        IConfiguration configuration = Build(new Dictionary<string, string?>
        {
            ["CommonSecrets:Mode"] = "Production",
            ["CommonSecrets:ProviderOrder:0"] = "PrimarySecrets",
            ["CommonSecrets:Providers:PrimarySecrets:Type"] = "OpenBao",
            ["CommonSecrets:Providers:PrimarySecrets:Settings:Enabled"] = "true",
            ["CommonSecrets:Providers:PrimarySecrets:Settings:RequireHttps"] = "false",
            ["CommonSecrets:Providers:PrimarySecrets:Settings:Address"] =
                "http://openbao.example:8200"
        });

        CommonSecretsOptions options =
            configuration.GetSection("CommonSecrets")
                .Get<CommonSecretsOptions>()!;

        IReadOnlyDictionary<string, SecretProviderDefinition> providers =
            SecretProviderConfiguration.ReadProviders(configuration);

        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(
                () => CommonSecretsPolicy.Validate(options, providers));

        Assert.Contains(
            "HTTPS",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void OfflineDevelopmentRejectsRemoteOpenBao()
    {
        IConfiguration configuration = Build(new Dictionary<string, string?>
        {
            ["CommonSecrets:Mode"] = "OfflineDevelopment",
            ["CommonSecrets:ProviderOrder:0"] = "LocalVault",
            ["CommonSecrets:Providers:LocalVault:Type"] = "OpenBao",
            ["CommonSecrets:Providers:LocalVault:Settings:Enabled"] = "true",
            ["CommonSecrets:Providers:LocalVault:Settings:RequireHttps"] = "false",
            ["CommonSecrets:Providers:LocalVault:Settings:Address"] =
                "https://openbao.office.example:8200"
        });

        CommonSecretsOptions options =
            configuration.GetSection("CommonSecrets")
                .Get<CommonSecretsOptions>()!;

        IReadOnlyDictionary<string, SecretProviderDefinition> providers =
            SecretProviderConfiguration.ReadProviders(configuration);

        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(
                () => CommonSecretsPolicy.Validate(options, providers));

        Assert.Contains(
            "loopback",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    private static IConfiguration Build(
        Dictionary<string, string?> values)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
}
