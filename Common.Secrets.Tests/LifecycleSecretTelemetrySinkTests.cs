using Common.Diagnostics;
using Common.Secrets;
using Xunit;

public sealed class LifecycleSecretTelemetrySinkTests
{
    [Fact]
    public async Task Audit_DoesNotEmitSecretNameOrValue()
    {
        var sink = new RecordingSink();
        var adapter = new LifecycleSecretTelemetrySink("Aegis.Hello", "hello-01", sink);

        await adapter.WriteAuditAsync(new SecretAuditEvent(
            DateTimeOffset.UtcNow,
            "Aegis.Hello",
            "Production",
            "hello-01",
            "Provider",
            "Payroll:DatabasePassword",
            "Get",
            "Success",
            TimeSpan.FromMilliseconds(5),
            "corr-1"));

        LifecycleEvent item = Assert.Single(sink.Items);
        string json = System.Text.Json.JsonSerializer.Serialize(item);
        Assert.DoesNotContain("Payroll:DatabasePassword", json, StringComparison.Ordinal);
        Assert.Equal("Secrets", item.Flow);
        Assert.Equal("Get", item.Stage);
    }

    private sealed class RecordingSink : ILifecycleEventSink
    {
        public List<LifecycleEvent> Items { get; } = [];
        public Task EmitAsync(LifecycleEvent lifecycleEvent, CancellationToken cancellationToken = default)
        {
            Items.Add(lifecycleEvent);
            return Task.CompletedTask;
        }
    }
}
