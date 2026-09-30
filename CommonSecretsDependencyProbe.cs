namespace Common.Secrets;

using Common.Diagnostics;
using System.Diagnostics;

public sealed class CommonSecretsDependencyProbe(
    ICommonSecretsHealthCheck healthCheck) : IDependencyDiagnosticProbe
{
    private readonly ICommonSecretsHealthCheck healthCheck =
        healthCheck ?? throw new ArgumentNullException(nameof(healthCheck));

    public string Component => "Common.Secrets";
    public string Dependency => "Configured secret provider";
    public DependencyDiagnosticKind Kind => DependencyDiagnosticKind.Secrets;
    public EngineeringDiagnosticLevel Level => EngineeringDiagnosticLevel.Level4Analysis;

    public async Task<DependencyVerificationResult> VerifyAsync(
        CancellationToken cancellationToken = default)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        try
        {
            CommonSecretsHealth health =
                await healthCheck.CheckAsync(cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();

            OperationalDiagnosticState state =
                !health.IsConfigured
                    ? OperationalDiagnosticState.Warning
                    : health.IsAvailable
                        ? OperationalDiagnosticState.Healthy
                        : OperationalDiagnosticState.Failed;

            return new DependencyVerificationResult(
                Component,
                string.IsNullOrWhiteSpace(health.ProviderName)
                    ? Dependency
                    : health.ProviderName,
                Kind,
                health.IsConfigured,
                health.IsAvailable,
                health.IsAvailable,
                state,
                DateTimeOffset.UtcNow,
                stopwatch.Elapsed,
                health.Message,
                "Secret values are never included in diagnostic evidence.",
                !health.IsConfigured
                    ? "SECRETS_NOT_CONFIGURED"
                    : health.IsAvailable
                        ? null
                        : "SECRETS_PROVIDER_UNAVAILABLE");
        }
        catch (Exception exception) when (
            exception is not OperationCanceledException ||
            !cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            return new DependencyVerificationResult(
                Component,
                Dependency,
                Kind,
                true,
                false,
                false,
                OperationalDiagnosticState.Failed,
                DateTimeOffset.UtcNow,
                stopwatch.Elapsed,
                "Secret provider verification failed.",
                $"FailureType={exception.GetType().Name}",
                "SECRETS_PROBE_FAILED");
        }
    }
}
