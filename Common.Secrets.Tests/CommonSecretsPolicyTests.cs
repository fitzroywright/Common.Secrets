using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Common.Secrets.Tests;

public sealed class CommonSecretsPolicyTests
{
    [Fact]
    public void ProductionDefaultsToOpenBaoOnly()
    {
        CommonSecretsOptions options = new();

        string[] providers = CommonSecretsPolicy.ResolveProviderOrder(options);

        Assert.Equal(["OpenBao"], providers);
    }

    [Fact]
    public void ProductionRejectsDeveloperFallbackProviders()
    {
        CommonSecretsOptions options = new()
        {
            Mode = SecretsEnvironmentMode.Production,
            ProviderOrder = ["OpenBao", "Configuration"]
        };

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => CommonSecretsPolicy.ResolveProviderOrder(options));

        Assert.Contains("not allowed", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ProductionRequiresOpenBaoAndHttps()
    {
        CommonSecretsOptions options = new() { Mode = SecretsEnvironmentMode.Production };
        OpenBaoOptions disabled = new() { Enabled = false, RequireHttps = true };
        OpenBaoOptions insecure = new() { Enabled = true, RequireHttps = false };

        Assert.Throws<InvalidOperationException>(() => CommonSecretsPolicy.Validate(options, disabled));
        Assert.Throws<InvalidOperationException>(() => CommonSecretsPolicy.Validate(options, insecure));
    }

    [Fact]
    public void OfflineDevelopmentRejectsRemoteOpenBao()
    {
        CommonSecretsOptions options = new()
        {
            Mode = SecretsEnvironmentMode.OfflineDevelopment
        };
        OpenBaoOptions openBao = new()
        {
            Enabled = true,
            Address = "https://openbao.office.example:8200",
            RequireHttps = true
        };

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => CommonSecretsPolicy.Validate(options, openBao));

        Assert.Contains("loopback", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void OfflineDevelopmentAllowsLocalOpenBao()
    {
        CommonSecretsOptions options = new()
        {
            Mode = SecretsEnvironmentMode.OfflineDevelopment
        };
        OpenBaoOptions openBao = new()
        {
            Enabled = true,
            Address = "http://127.0.0.1:8200",
            RequireHttps = false
        };

        CommonSecretsPolicy.Validate(options, openBao);
    }

    [Fact]
    public void ServiceRegistrationFailsClosedForProductionFallbackConfiguration()
    {
        Dictionary<string, string?> values = new()
        {
            ["CommonSecrets:Mode"] = "Production",
            ["CommonSecrets:ProviderOrder:0"] = "OpenBao",
            ["CommonSecrets:ProviderOrder:1"] = "Configuration",
            ["CommonSecrets:OpenBao:Enabled"] = "true",
            ["CommonSecrets:OpenBao:RequireHttps"] = "true",
            ["CommonSecrets:OpenBao:Address"] = "https://openbao.example:8200"
        };
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
        ServiceCollection services = new();

        Assert.Throws<InvalidOperationException>(() => services.AddCommonSecrets(configuration));
    }
}
