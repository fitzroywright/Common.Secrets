using Microsoft.Extensions.Logging;

namespace Common.Secrets;

public sealed class CommonDiagnosticsSecretTelemetrySink : ISecretTelemetrySink
{
    private readonly ILogger<CommonDiagnosticsSecretTelemetrySink> logger;

    public CommonDiagnosticsSecretTelemetrySink(ILogger<CommonDiagnosticsSecretTelemetrySink> logger)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public ValueTask WriteAuditAsync(
        SecretAuditEvent auditEvent,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Secret audit {Workload} {Environment} {Machine} {Provider} {SecretName} {Operation} {Result} {DurationMs} {CorrelationId}",
            auditEvent.Workload,
            auditEvent.Environment,
            auditEvent.Machine,
            auditEvent.Provider,
            auditEvent.SecretName,
            auditEvent.Operation,
            auditEvent.Result,
            auditEvent.Duration.TotalMilliseconds,
            auditEvent.CorrelationId);

        return ValueTask.CompletedTask;
    }

    public ValueTask WriteDiagnosticAsync(
        SecretDiagnosticEvent diagnosticEvent,
        CancellationToken cancellationToken = default)
    {
        LogLevel level = diagnosticEvent.Severity switch
        {
            SecretTelemetrySeverity.Information => LogLevel.Information,
            SecretTelemetrySeverity.Warning => LogLevel.Warning,
            SecretTelemetrySeverity.Error => LogLevel.Error,
            _ => LogLevel.Information
        };

        logger.Log(
            level,
            new EventId(GetEventId(diagnosticEvent.Code), diagnosticEvent.Code),
            "Secret diagnostic {Code} {Provider} {Message} {SecretName} {CorrelationId} {ExceptionType}",
            diagnosticEvent.Code,
            diagnosticEvent.Provider,
            diagnosticEvent.Message,
            diagnosticEvent.SecretName,
            diagnosticEvent.CorrelationId,
            diagnosticEvent.ExceptionType);

        return ValueTask.CompletedTask;
    }

    private static int GetEventId(string code)
    {
        if (code.StartsWith("SECRETS", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(code[7..], out int value))
        {
            return 7000 + value;
        }

        return 7000;
    }
}
