using System.Net;
using System.Text;
using Xunit;

namespace Common.Secrets.Tests;

public sealed class OpenBaoSecretProviderTests
{
    [Fact]
    public async Task GetAsync_UsesAuthenticatorTokenAndReadsKvValue()
    {
        RecordingHandler handler = new(HttpStatusCode.OK, "{\"data\":{\"data\":{\"value\":\"super-secret\"}}}");
        using HttpClient client = new(handler);
        OpenBaoOptions options = new()
        {
            Enabled = true,
            Address = "https://openbao.internal:8200",
            MountPath = "secret",
            BasePath = "aegis/studio"
        };
        StubAuthenticator authenticator = new("short-lived-token");
        OpenBaoSecretProvider provider = new(options, client, authenticator);

        string? result = await provider.GetAsync("Database:Password");

        Assert.Equal("super-secret", result);
        Assert.Equal("short-lived-token", handler.LastToken);
        Assert.Equal(1, authenticator.CallCount);
    }

    [Fact]
    public async Task GetAsync_ReturnsNullWhenSecretDoesNotExist()
    {
        RecordingHandler handler = new(HttpStatusCode.NotFound, "{}");
        using HttpClient client = new(handler);
        OpenBaoSecretProvider provider = new(
            new OpenBaoOptions { Enabled = true, Address = "https://openbao.internal:8200" },
            client,
            new StubAuthenticator("short-lived-token"));

        string? result = await provider.GetAsync("Missing:Secret");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetAsync_DoesNothingWhenProviderIsDisabled()
    {
        RecordingHandler handler = new(HttpStatusCode.InternalServerError, "{}");
        using HttpClient client = new(handler);
        StubAuthenticator authenticator = new("unused");
        OpenBaoSecretProvider provider = new(new OpenBaoOptions(), client, authenticator);

        string? result = await provider.GetAsync("Database:Password");

        Assert.Null(result);
        Assert.Equal(0, authenticator.CallCount);
    }

    private sealed class StubAuthenticator : IOpenBaoAuthenticator
    {
        private readonly string token;

        public StubAuthenticator(string token)
        {
            this.token = token;
        }

        public int CallCount { get; private set; }

        public Task<string> GetTokenAsync(CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(token);
        }
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode statusCode;
        private readonly string responseBody;

        public RecordingHandler(HttpStatusCode statusCode, string responseBody)
        {
            this.statusCode = statusCode;
            this.responseBody = responseBody;
        }

        public string? LastToken { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastToken = request.Headers.TryGetValues("X-Vault-Token", out IEnumerable<string>? values)
                ? values.SingleOrDefault()
                : null;
            return Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
            });
        }
    }
}
