namespace Common.Secrets.Tests;

public sealed class ObservedSecretProviderTests
{
    [Fact]
    public async Task GetAsync_WritesStructuredAuditWithoutSecretValue()
    {
        const string secretValue = "Password123!";
        RecordingTelemetrySink sink = new();
        ObservedSecretProvider provider = new(
            new FixedProvider(secretValue),
            sink,
            new CommonSecretsOptions
            {
                WorkloadName = "Cafeteria",
                EnvironmentName = "Test",
                MachineName = "TEST01"
            });

        string? value = await provider.GetAsync("cafeteria/database");

        Assert.Equal(secretValue, value);
        SecretAuditEvent audit = Assert.Single(sink.AuditEvents);
        Assert.Equal("Cafeteria", audit.Workload);
        Assert.Equal("cafeteria/database", audit.SecretName);
        Assert.Equal("Success", audit.Result);
        Assert.DoesNotContain(secretValue, Serialize(sink));
    }

    [Fact]
    public async Task GetAsync_FailureDiagnosticDoesNotContainExceptionMessageOrSecret()
    {
        const string secretValue = "postgres://user:Password123!@server/database";
        RecordingTelemetrySink sink = new();
        ObservedSecretProvider provider = new(
            new ThrowingProvider(new InvalidOperationException($"Connection failed using {secretValue}")),
            sink,
            new CommonSecretsOptions());

        await Assert.ThrowsAsync<InvalidOperationException>(() => provider.GetAsync("cafeteria/database"));

        SecretDiagnosticEvent diagnostic = Assert.Single(sink.DiagnosticEvents);
        Assert.Equal("SECRETS031", diagnostic.Code);
        Assert.Equal(typeof(InvalidOperationException).FullName, diagnostic.ExceptionType);
        Assert.DoesNotContain(secretValue, Serialize(sink));
        Assert.DoesNotContain("Connection failed", Serialize(sink));
    }

    [Fact]
    public async Task GetAsync_NotFoundIsAuditedWithoutInventingAValue()
    {
        RecordingTelemetrySink sink = new();
        ObservedSecretProvider provider = new(
            new FixedProvider(null),
            sink,
            new CommonSecretsOptions());

        string? value = await provider.GetAsync("studio/graph");

        Assert.Null(value);
        Assert.Equal("NotFound", Assert.Single(sink.AuditEvents).Result);
        Assert.Equal("SECRETS022", Assert.Single(sink.DiagnosticEvents).Code);
    }

    private static string Serialize(RecordingTelemetrySink sink)
    {
        return System.Text.Json.JsonSerializer.Serialize(new
        {
            sink.AuditEvents,
            sink.DiagnosticEvents
        });
    }

    private sealed class FixedProvider(string? value) : SecretProviderBase
    {
        public override Task<string?> GetAsync(string name, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(value);
        }
    }

    private sealed class ThrowingProvider(Exception exception) : SecretProviderBase
    {
        public override Task<string?> GetAsync(string name, CancellationToken cancellationToken = default)
        {
            return Task.FromException<string?>(exception);
        }
    }

    private sealed class RecordingTelemetrySink : ISecretTelemetrySink
    {
        public List<SecretAuditEvent> AuditEvents { get; } = [];

        public List<SecretDiagnosticEvent> DiagnosticEvents { get; } = [];

        public ValueTask WriteAuditAsync(SecretAuditEvent auditEvent, CancellationToken cancellationToken = default)
        {
            AuditEvents.Add(auditEvent);
            return ValueTask.CompletedTask;
        }

        public ValueTask WriteDiagnosticAsync(SecretDiagnosticEvent diagnosticEvent, CancellationToken cancellationToken = default)
        {
            DiagnosticEvents.Add(diagnosticEvent);
            return ValueTask.CompletedTask;
        }
    }
}
