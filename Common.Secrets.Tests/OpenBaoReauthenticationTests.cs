using System.Net;
using System.Text;
using Xunit;

namespace Common.Secrets.Tests;

public sealed class OpenBaoReauthenticationTests
{
    [Fact]
    public async Task RenewalFailure_ReauthenticatesAfterBootstrapFileHasBeenDeleted()
    {
        string path = Path.Combine(Path.GetTempPath(), $"common-secrets-reauth-{Guid.NewGuid():N}.secretid");
        await File.WriteAllTextAsync(path, "protected-secret-id");

        try
        {
            ReauthenticationHandler handler = new();
            using HttpClient client = new(handler);
            OpenBaoOptions options = new()
            {
                Enabled = true,
                Address = "https://openbao.internal:8200",
                RoleId = "cafeteria-role",
                SecretIdFile = path,
                RequireSecretIdFileInProduction = true,
                DeleteSecretIdFileAfterRead = true,
                RenewTokens = true,
                TokenRenewalSafetyWindow = TimeSpan.FromMilliseconds(1500),
                RevokeTokenOnDispose = false
            };
            await using OpenBaoAuthenticator authenticator = new(
                options,
                new CommonSecretsOptions { Mode = SecretsEnvironmentMode.Production },
                client);

            string first = await authenticator.GetTokenAsync();
            Assert.Equal("token-1", first);
            Assert.False(File.Exists(path));

            await Task.Delay(TimeSpan.FromMilliseconds(1200));
            string second = await authenticator.GetTokenAsync();

            Assert.Equal("token-2", second);
            Assert.Equal(2, handler.LoginCalls);
            Assert.Equal(1, handler.RenewCalls);
            Assert.All(handler.LoginBodies, body => Assert.Contains("protected-secret-id", body, StringComparison.Ordinal));
            Assert.False(File.Exists(path));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private sealed class ReauthenticationHandler : HttpMessageHandler
    {
        public int LoginCalls { get; private set; }
        public int RenewCalls { get; private set; }
        public List<string> LoginBodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            string path = request.RequestUri?.AbsolutePath ?? string.Empty;
            if (path.EndsWith("/login", StringComparison.Ordinal))
            {
                LoginCalls++;
                LoginBodies.Add(request.Content is null
                    ? string.Empty
                    : await request.Content.ReadAsStringAsync(cancellationToken));
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        $"{{\"auth\":{{\"client_token\":\"token-{LoginCalls}\",\"lease_duration\":2,\"renewable\":true}}}}",
                        Encoding.UTF8,
                        "application/json")
                };
            }

            if (path.EndsWith("/renew-self", StringComparison.Ordinal))
            {
                RenewCalls++;
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }
    }
}
