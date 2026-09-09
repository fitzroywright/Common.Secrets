namespace Common.Secrets;

public enum SecretTelemetrySeverity
{
    Information = 0,
    Warning = 1,
    Error = 2
}

public sealed record SecretAuditEvent(
    DateTimeOffset Timestamp,
    string Workload,
    string Environment,
    string Machine,
    string Provider,
    string SecretName,
    string Operation,
    string Result,
    TimeSpan Duration,
    string CorrelationId);

public sealed record SecretDiagnosticEvent(
    DateTimeOffset Timestamp,
    string Code,
    SecretTelemetrySeverity Severity,
    string Provider,
    string Message,
    string? SecretName = null,
    string? CorrelationId = null,
    string? ExceptionType = null);

public interface ISecretTelemetrySink
{
    ValueTask WriteAuditAsync(
        SecretAuditEvent auditEvent,
        CancellationToken cancellationToken = default);

    ValueTask WriteDiagnosticAsync(
        SecretDiagnosticEvent diagnosticEvent,
        CancellationToken cancellationToken = default);
}

public sealed class NullSecretTelemetrySink : ISecretTelemetrySink
{
    public ValueTask WriteAuditAsync(
        SecretAuditEvent auditEvent,
        CancellationToken cancellationToken = default)
    {
        return ValueTask.CompletedTask;
    }

    public ValueTask WriteDiagnosticAsync(
        SecretDiagnosticEvent diagnosticEvent,
        CancellationToken cancellationToken = default)
    {
        return ValueTask.CompletedTask;
    }
}
