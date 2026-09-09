using Common.Diagnostics;
using Xunit;

namespace Common.Secrets.Tests;

public sealed class CommonSecretsDiagnosticCheckTests
{
    [Fact]
    public async Task RunAsync_ReturnsHealthyWhenEnabledProvidersAreAvailable()
    {
        CommonSecretsDiagnosticCheck check = new([
            new StubHealth("OpenBao", true, true),
            new StubHealth("Bitwarden", false, false)
        ]);

        DiagnosticResult result = await check.RunAsync();

        Assert.Equal(DiagnosticStatus.Healthy, result.Status);
        Assert.Contains("OpenBao", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_ReturnsUnhealthyWithoutLeakingProviderExceptionMessage()
    {
        const string secret = "Password123!";
        CommonSecretsDiagnosticCheck check = new([
            new StubHealth(
                "OpenBao",
                true,
                false,
                new InvalidOperationException($"failed with {secret}"))
        ]);

        DiagnosticResult result = await check.RunAsync();

        Assert.Equal(DiagnosticStatus.Unhealthy, result.Status);
        Assert.DoesNotContain(secret, result.Message, StringComparison.Ordinal);
        Assert.Null(result.Exception);
    }

    private sealed class StubHealth(
        string providerName,
        bool enabled,
        bool available,
        Exception? exception = null) : ISecretProviderHealth
    {
        public string ProviderName => providerName;

        public bool IsEnabled => enabled;

        public Exception? LastError => exception;

        public Task<SecretProviderHealth> CheckHealthAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new SecretProviderHealth(
                providerName,
                enabled,
                available,
                available ? "healthy" : "failed",
                exception));
        }
    }
}
