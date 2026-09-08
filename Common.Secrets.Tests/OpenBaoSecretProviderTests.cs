using System.Net;
using System.Text;
using Xunit;

namespace Common.Secrets.Tests;

public sealed class OpenBaoSecretProviderTests
{
    [Fact]
    public async Task GetAsync_ReadsValueFromKvV2Path()
    {
        RecordingHandler handler = new(HttpStatusCode.OK, "{\"data\":{\"data\":{\"value\":\"super-secret\"}}}");
        using HttpClient client = new(handler);
        OpenBaoOptions options = new()
        {
            Enabled = true,
            Address = "https://openbao.internal:8200",
            MountPath = "secret",
            BasePath = "aegis/studio",
            Token = "test-token"
        };
        OpenBaoSecretProvider provider = new(options, client);

        string? result = await provider.GetAsync("Database:Password");

        Assert.Equal("super-secret", result);
        Assert.Equal(
            "https://openbao.internal:8200/v1/secret/data/aegis/studio/Database/Password",
            handler.LastRequestUri?.ToString());
        Assert.Equal("test-token", handler.LastToken);
    }

    [Fact]
    public async Task GetAsync_ReturnsNullWhenSecretDoesNotExist()
    {
        RecordingHandler handler = new(HttpStatusCode.NotFound, "{}");
        using HttpClient client = new(handler);
        OpenBaoOptions options = new()
        {
            Enabled = true,
            Address = "https://openbao.internal:8200",
            Token = "test-token"
        };
        OpenBaoSecretProvider provider = new(options, client);

        string? result = await provider.GetAsync("Missing:Secret");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetAsync_DoesNothingWhenProviderIsDisabled()
    {
        RecordingHandler handler = new(HttpStatusCode.InternalServerError, "{}");
        using HttpClient client = new(handler);
        OpenBaoSecretProvider provider = new(new OpenBaoOptions(), client);

        string? result = await provider.GetAsync("Database:Password");

        Assert.Null(result);
        Assert.Null(handler.LastRequestUri);
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

        public Uri? LastRequestUri { get; private set; }

        public string? LastToken { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;
            LastToken = request.Headers.TryGetValues("X-Vault-Token", out IEnumerable<string>? values)
                ? values.SingleOrDefault()
                : null;

            HttpResponseMessage response = new(statusCode)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }
}
