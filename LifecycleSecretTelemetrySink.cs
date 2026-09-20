using Common.Diagnostics;

namespace Common.Secrets;

public sealed class LifecycleSecretTelemetrySink : ISecretTelemetrySink
{
    private readonly string applicationId;
    private readonly string instanceId;
    private readonly ILifecycleEventSink lifecycle;

    public LifecycleSecretTelemetrySink(
        string applicationId,
        string instanceId,
        ILifecycleEventSink lifecycle)
    {
        if (string.IsNullOrWhiteSpace(applicationId)) throw new ArgumentException("Application id is required.", nameof(applicationId));
        if (string.IsNullOrWhiteSpace(instanceId)) throw new ArgumentException("Instance id is required.", nameof(instanceId));
        this.applicationId = applicationId.Trim();
        this.instanceId = instanceId.Trim();
        this.lifecycle = lifecycle ?? throw new ArgumentNullException(nameof(lifecycle));
    }

    public async ValueTask WriteAuditAsync(
        SecretAuditEvent auditEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);
        LifecycleEventOutcome outcome = IsSuccess(auditEvent.Result)
            ? LifecycleEventOutcome.Succeeded
            : LifecycleEventOutcome.Warning;

        await TryEmitAsync(
            LifecycleEvent.Create(
                applicationId,
                instanceId,
                "Secrets",
                auditEvent.Operation,
                outcome,
                auditEvent.CorrelationId,
                properties: new Dictionary<string, string>
                {
                    ["Provider"] = auditEvent.Provider,
                    ["Result"] = auditEvent.Result
                },
                occurredAtUtc: auditEvent.Timestamp),
            cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask WriteDiagnosticAsync(
        SecretDiagnosticEvent diagnosticEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(diagnosticEvent);
        LifecycleEventOutcome outcome = diagnosticEvent.Severity switch
        {
            SecretTelemetrySeverity.Error => LifecycleEventOutcome.Failed,
            SecretTelemetrySeverity.Warning => LifecycleEventOutcome.Warning,
            _ => LifecycleEventOutcome.Succeeded
        };

        await TryEmitAsync(
            LifecycleEvent.Create(
                applicationId,
                instanceId,
                "Secrets",
                "Diagnostic",
                outcome,
                string.IsNullOrWhiteSpace(diagnosticEvent.CorrelationId)
                    ? Guid.NewGuid().ToString("N")
                    : diagnosticEvent.CorrelationId,
                code: diagnosticEvent.Code,
                properties: new Dictionary<string, string>
                {
                    ["Provider"] = diagnosticEvent.Provider
                },
                occurredAtUtc: diagnosticEvent.Timestamp),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task TryEmitAsync(LifecycleEvent item, CancellationToken cancellationToken)
    {
        try
        {
            await lifecycle.EmitAsync(item, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            // Secret access must not fail because telemetry is unavailable.
        }
    }

    private static bool IsSuccess(string result) =>
        result.Equals("Success", StringComparison.OrdinalIgnoreCase) ||
        result.Equals("Succeeded", StringComparison.OrdinalIgnoreCase) ||
        result.Equals("Resolved", StringComparison.OrdinalIgnoreCase) ||
        result.Equals("Found", StringComparison.OrdinalIgnoreCase);
}
