using System.Diagnostics;

namespace Common.Secrets;

public sealed class ObservedSecretProvider : SecretProviderBase, ISecretProviderHealth
{
    private readonly ISecretProvider inner;
    private readonly ISecretTelemetrySink telemetry;
    private readonly CommonSecretsOptions options;
    private Exception? lastError;

    public ObservedSecretProvider(
        ISecretProvider inner,
        ISecretTelemetrySink telemetry,
        CommonSecretsOptions options)
    {
        this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
        this.telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
        this.options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public string ProviderName => inner is ISecretProviderHealth health
        ? health.ProviderName
        : inner.GetType().Name;

    public bool IsEnabled => inner is not ISecretProviderHealth health || health.IsEnabled;

    public Exception? LastError => lastError ?? (inner as ISecretProviderHealth)?.LastError;

    public override async Task<string?> GetAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        string correlationId = Guid.NewGuid().ToString("N");
        Stopwatch stopwatch = Stopwatch.StartNew();

        try
        {
            string? value = await inner.GetAsync(name, cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();
            lastError = null;

            await telemetry.WriteAuditAsync(
                CreateAudit(name, value is null ? "NotFound" : "Success", stopwatch.Elapsed, correlationId),
                cancellationToken).ConfigureAwait(false);

            await telemetry.WriteDiagnosticAsync(
                new SecretDiagnosticEvent(
                    DateTimeOffset.UtcNow,
                    value is null ? "SECRETS022" : "SECRETS021",
                    value is null ? SecretTelemetrySeverity.Warning : SecretTelemetrySeverity.Information,
                    ProviderName,
                    value is null ? "Secret was not found." : "Secret retrieval succeeded.",
                    name,
                    correlationId),
                cancellationToken).ConfigureAwait(false);

            return value;
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            lastError = exception;

            await telemetry.WriteAuditAsync(
                CreateAudit(name, "Failed", stopwatch.Elapsed, correlationId),
                cancellationToken).ConfigureAwait(false);

            await telemetry.WriteDiagnosticAsync(
                new SecretDiagnosticEvent(
                    DateTimeOffset.UtcNow,
                    "SECRETS031",
                    SecretTelemetrySeverity.Error,
                    ProviderName,
                    "Secret provider operation failed.",
                    name,
                    correlationId,
                    exception.GetType().FullName),
                cancellationToken).ConfigureAwait(false);

            throw;
        }
    }

    public async Task<SecretProviderHealth> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        if (inner is not ISecretProviderHealth health)
        {
            return new SecretProviderHealth(ProviderName, true, true, "Provider does not expose a health probe.");
        }

        string correlationId = Guid.NewGuid().ToString("N");
        SecretProviderHealth result = await health.CheckHealthAsync(cancellationToken).ConfigureAwait(false);
        lastError = result.Exception;

        await telemetry.WriteDiagnosticAsync(
            new SecretDiagnosticEvent(
                DateTimeOffset.UtcNow,
                result.IsAvailable ? "SECRETS001" : "SECRETS032",
                result.IsAvailable ? SecretTelemetrySeverity.Information : SecretTelemetrySeverity.Error,
                result.ProviderName,
                result.IsAvailable ? "Secret provider health check succeeded." : "Secret provider health check failed.",
                CorrelationId: correlationId,
                ExceptionType: result.Exception?.GetType().FullName),
            cancellationToken).ConfigureAwait(false);

        return result with
        {
            Message = result.IsAvailable ? "Provider is available." : "Provider is unavailable.",
            Exception = result.Exception
        };
    }

    private SecretAuditEvent CreateAudit(
        string name,
        string result,
        TimeSpan duration,
        string correlationId)
    {
        string workload = string.IsNullOrWhiteSpace(options.WorkloadName)
            ? "Unknown"
            : options.WorkloadName.Trim();
        string environment = string.IsNullOrWhiteSpace(options.EnvironmentName)
            ? "Unknown"
            : options.EnvironmentName.Trim();
        string machine = string.IsNullOrWhiteSpace(options.MachineName)
            ? Environment.MachineName
            : options.MachineName.Trim();

        return new SecretAuditEvent(
            DateTimeOffset.UtcNow,
            workload,
            environment,
            machine,
            ProviderName,
            name,
            "Read",
            result,
            duration,
            correlationId);
    }
}
