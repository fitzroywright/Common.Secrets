using System.Diagnostics;
using Common.Diagnostics;

namespace Common.Secrets;

public sealed class CommonSecretsDiagnosticCheck : IDiagnosticCheck
{
    private readonly IReadOnlyList<ISecretProviderHealth> providers;

    public CommonSecretsDiagnosticCheck(IEnumerable<ISecretProviderHealth> providers)
    {
        this.providers = (providers ?? throw new ArgumentNullException(nameof(providers))).ToArray();
    }

    public string Name => "Common.Secrets";

    public async Task<DiagnosticResult> RunAsync(CancellationToken cancellationToken = default)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();

        if (providers.Count == 0)
        {
            stopwatch.Stop();
            return new DiagnosticResult(
                Name,
                DiagnosticStatus.Warning,
                "No secret provider health probes are registered.",
                stopwatch.Elapsed);
        }

        List<SecretProviderHealth> results = [];
        foreach (ISecretProviderHealth provider in providers)
        {
            results.Add(await provider.CheckHealthAsync(cancellationToken).ConfigureAwait(false));
        }

        stopwatch.Stop();
        SecretProviderHealth[] enabled = results.Where(result => result.IsEnabled).ToArray();
        SecretProviderHealth[] unavailable = enabled.Where(result => !result.IsAvailable).ToArray();

        if (unavailable.Length > 0)
        {
            string names = string.Join(", ", unavailable.Select(result => result.ProviderName));
            return new DiagnosticResult(
                Name,
                DiagnosticStatus.Unhealthy,
                $"Secret provider health failed: {names}.",
                stopwatch.Elapsed);
        }

        if (enabled.Length == 0)
        {
            return new DiagnosticResult(
                Name,
                DiagnosticStatus.Warning,
                "All registered secret providers are disabled.",
                stopwatch.Elapsed);
        }

        string healthyNames = string.Join(", ", enabled.Select(result => result.ProviderName));
        return new DiagnosticResult(
            Name,
            DiagnosticStatus.Healthy,
            $"Secret providers healthy: {healthyNames}.",
            stopwatch.Elapsed);
    }
}
