using System.Net;
using System.Text;
using Xunit;

namespace Common.Secrets.Tests;

public sealed class OpenBaoAuthenticatorTests
{
    [Fact]
    public async Task ProductionUsesWorkloadLoginInsteadOfConfiguredDevelopmentToken()
    {
        const string secretIdVariable = "COMMON_SECRETS_TEST_SECRET_ID";
        string? previous = Environment.GetEnvironmentVariable(secretIdVariable);
        Environment.SetEnvironmentVariable(secretIdVariable, "protected-secret-id");

        try
        {
            AuthHandler handler = new();
            using HttpClient client = new(handler);
            OpenBaoOptions options = new()
            {
                Enabled = true,
                Address = "https://openbao.internal:8200",
                RoleId = "cafeteria-role",
                SecretIdEnvironmentVariable = secretIdVariable,
                DevelopmentToken = "must-not-be-used"
            };
            CommonSecretsOptions common = new() { Mode = SecretsEnvironmentMode.Production };
            await using OpenBaoAuthenticator authenticator = new(options, common, client);

            string token = await authenticator.GetTokenAsync();

            Assert.Equal("issued-short-lived-token", token);
            Assert.Contains("cafeteria-role", handler.RequestBody, StringComparison.Ordinal);
            Assert.Contains("protected-secret-id", handler.RequestBody, StringComparison.Ordinal);
            Assert.DoesNotContain("must-not-be-used", handler.RequestBody, StringComparison.Ordinal);
            Assert.Null(Environment.GetEnvironmentVariable(secretIdVariable));
        }
        finally
        {
            Environment.SetEnvironmentVariable(secretIdVariable, previous);
        }
    }

    [Fact]
    public async Task ProductionFallsBackToEnvironmentWhenOptionalSecretIdFileIsMissing()
    {
        const string secretIdVariable = "COMMON_SECRETS_TEST_FALLBACK_SECRET_ID";
        string path = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.secretid");
        string? previous = Environment.GetEnvironmentVariable(secretIdVariable);
        Environment.SetEnvironmentVariable(secretIdVariable, "environment-secret-id");

        try
        {
            AuthHandler handler = new();
            using HttpClient client = new(handler);
            OpenBaoOptions options = new()
            {
                Enabled = true,
                Address = "https://openbao.internal:8200",
                RoleId = "requestportal-role",
                SecretIdFile = path,
                SecretIdEnvironmentVariable = secretIdVariable,
                RequireSecretIdFileInProduction = false,
                RevokeTokenOnDispose = false
            };
            await using OpenBaoAuthenticator authenticator = new(
                options,
                new CommonSecretsOptions { Mode = SecretsEnvironmentMode.Production },
                client);

            string token = await authenticator.GetTokenAsync();

            Assert.Equal("issued-short-lived-token", token);
            Assert.Contains("environment-secret-id", handler.RequestBody, StringComparison.Ordinal);
            Assert.Null(Environment.GetEnvironmentVariable(secretIdVariable));
        }
        finally
        {
            Environment.SetEnvironmentVariable(secretIdVariable, previous);
        }
    }

    [Fact]
    public async Task ProductionCanRequireOneTimeProtectedSecretIdFile()
    {
        string path = Path.Combine(Path.GetTempPath(), $"common-secrets-{Guid.NewGuid():N}.secretid");
        await File.WriteAllTextAsync(path, "one-time-secret-id");

        try
        {
            AuthHandler handler = new();
            using HttpClient client = new(handler);
            OpenBaoOptions options = new()
            {
                Enabled = true,
                Address = "https://openbao.internal:8200",
                RoleId = "studio-role",
                SecretIdFile = path,
                RequireSecretIdFileInProduction = true,
                DeleteSecretIdFileAfterRead = true,
                RevokeTokenOnDispose = false
            };
            await using OpenBaoAuthenticator authenticator = new(
                options,
                new CommonSecretsOptions { Mode = SecretsEnvironmentMode.Production },
                client);

            string token = await authenticator.GetTokenAsync();

            Assert.Equal("issued-short-lived-token", token);
            Assert.Contains("one-time-secret-id", handler.RequestBody, StringComparison.Ordinal);
            Assert.False(File.Exists(path));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task ProductionFailsClosedWhenProtectedFileIsRequiredButMissing()
    {
        const string secretIdVariable = "COMMON_SECRETS_TEST_REQUIRED_FILE_SECRET_ID";
        string path = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.secretid");
        string? previous = Environment.GetEnvironmentVariable(secretIdVariable);
        Environment.SetEnvironmentVariable(secretIdVariable, "must-not-fallback");

        try
        {
            using HttpClient client = new(new AuthHandler());
            OpenBaoOptions options = new()
            {
                Enabled = true,
                Address = "https://openbao.internal:8200",
                RoleId = "cafeteria-role",
                SecretIdFile = path,
                SecretIdEnvironmentVariable = secretIdVariable,
                RequireSecretIdFileInProduction = true,
                RevokeTokenOnDispose = false
            };
            await using OpenBaoAuthenticator authenticator = new(
                options,
                new CommonSecretsOptions { Mode = SecretsEnvironmentMode.Production },
                client);

            await Assert.ThrowsAsync<InvalidOperationException>(() => authenticator.GetTokenAsync());
            Assert.Equal("must-not-fallback", Environment.GetEnvironmentVariable(secretIdVariable));
        }
        finally
        {
            Environment.SetEnvironmentVariable(secretIdVariable, previous);
        }
    }

    [Fact]
    public async Task ProductionFailsWithoutWorkloadBootstrapIdentity()
    {
        const string secretIdVariable = "COMMON_SECRETS_TEST_MISSING_SECRET_ID";
        string? previous = Environment.GetEnvironmentVariable(secretIdVariable);
        Environment.SetEnvironmentVariable(secretIdVariable, null);

        try
        {
            using HttpClient client = new(new AuthHandler());
            OpenBaoOptions options = new()
            {
                Enabled = true,
                Address = "https://openbao.internal:8200",
                RoleId = "cafeteria-role",
                SecretIdEnvironmentVariable = secretIdVariable,
                DevelopmentToken = "configured-static-token",
                RevokeTokenOnDispose = false
            };
            await using OpenBaoAuthenticator authenticator = new(
                options,
                new CommonSecretsOptions { Mode = SecretsEnvironmentMode.Production },
                client);

            await Assert.ThrowsAsync<InvalidOperationException>(() => authenticator.GetTokenAsync());
        }
        finally
        {
            Environment.SetEnvironmentVariable(secretIdVariable, previous);
        }
    }

    private sealed class AuthHandler : HttpMessageHandler
    {
        public string RequestBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestBody = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"auth\":{\"client_token\":\"issued-short-lived-token\",\"lease_duration\":600,\"renewable\":true}}",
                    Encoding.UTF8,
                    "application/json")
            };
        }
    }
}
