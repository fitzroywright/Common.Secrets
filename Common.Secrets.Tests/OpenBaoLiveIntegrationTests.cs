namespace Common.Secrets.Tests;

using Xunit;

public sealed class OpenBaoLiveIntegrationTests
{
    [Fact]
    public async Task AppRoleProvider_ReadsSeededSecret_AndReportsHealthy()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("COMMON_SECRETS_LIVE_OPENBAO"), "1", StringComparison.Ordinal))
        {
            return;
        }

        string address = Environment.GetEnvironmentVariable("OPENBAO_ADDR")
            ?? throw new InvalidOperationException("OPENBAO_ADDR is required for the live OpenBao integration test.");
        string roleId = Environment.GetEnvironmentVariable("OPENBAO_ROLE_ID")
            ?? throw new InvalidOperationException("OPENBAO_ROLE_ID is required for the live OpenBao integration test.");
        string secretId = Environment.GetEnvironmentVariable("OPENBAO_SECRET_ID")
            ?? throw new InvalidOperationException("OPENBAO_SECRET_ID is required for the live OpenBao integration test.");

        Environment.SetEnvironmentVariable("COMMON_SECRETS_TEST_SECRET_ID", secretId);
        try
        {
            OpenBaoOptions options = new()
            {
                Enabled = true,
                Address = address,
                MountPath = "aegis-test",
                BasePath = "integration",
                AuthMountPath = "approle",
                RoleId = roleId,
                SecretIdEnvironmentVariable = "COMMON_SECRETS_TEST_SECRET_ID",
                ClearSecretIdEnvironmentVariableAfterRead = false,
                RequireHttps = false,
                RenewTokens = true,
                RevokeTokenOnDispose = true,
                TokenRenewalSafetyWindow = TimeSpan.FromSeconds(1),
                SecretCacheTtl = TimeSpan.FromSeconds(1)
            };
            CommonSecretsOptions commonOptions = new()
            {
                Mode = SecretsEnvironmentMode.Production
            };

            using HttpClient client = new();
            await using OpenBaoAuthenticator authenticator = new(options, commonOptions, client);
            using OpenBaoSecretProvider provider = new(options, client, authenticator);

            string? value = await provider.GetAsync("Smoke:Value");
            Assert.Equal("integration-ok", value);

            SecretProviderHealth health = await provider.CheckHealthAsync();
            Assert.True(health.IsEnabled);
            Assert.True(health.IsAvailable);

            await authenticator.RevokeAsync();
        }
        finally
        {
            Environment.SetEnvironmentVariable("COMMON_SECRETS_TEST_SECRET_ID", null);
        }
    }
}
