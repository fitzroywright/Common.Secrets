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
            OpenBaoAuthenticator authenticator = new(options, common, client);

            string token = await authenticator.GetTokenAsync();

            Assert.Equal("issued-short-lived-token", token);
            Assert.Contains("cafeteria-role", handler.RequestBody, StringComparison.Ordinal);
            Assert.Contains("protected-secret-id", handler.RequestBody, StringComparison.Ordinal);
            Assert.DoesNotContain("must-not-be-used", handler.RequestBody, StringComparison.Ordinal);
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
                DevelopmentToken = "configured-static-token"
            };
            OpenBaoAuthenticator authenticator = new(
                options,
                new CommonSecretsOptions { Mode = SecretsEnvironmentMode.Production },
                client);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => authenticator.GetTokenAsync());
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
                    "{\"auth\":{\"client_token\":\"issued-short-lived-token\",\"lease_duration\":600}}",
                    Encoding.UTF8,
                    "application/json")
            };
        }
    }
}
