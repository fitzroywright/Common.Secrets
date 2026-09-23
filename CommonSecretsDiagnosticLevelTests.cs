using Common.Diagnostics;

namespace Common.Secrets;

public sealed class CommonSecretsProviderRegistrationDiagnosticLevelTest : IDiagnosticLevelLocalTest
{
    private readonly IReadOnlyList<ISecretProviderHealth> providers;

    public CommonSecretsProviderRegistrationDiagnosticLevelTest(IEnumerable<ISecretProviderHealth> providers)
        => this.providers = (providers ?? throw new ArgumentNullException(nameof(providers))).ToArray();

    public string TestId => "COMMON.SECRETS.L5.PROVIDERS.REGISTERED";
    public string Name => "Secret provider health probes registered";
    public string Owner => "Common.Secrets";
    public EngineeringDiagnosticLevel Level => EngineeringDiagnosticLevel.Level5Scan;
    public bool IsDestructive => false;

    public Task<EngineeringDiagnosticCheckResult> RunAsync(CancellationToken cancellationToken)
    {
        if (providers.Count == 0)
            return Task.FromResult(EngineeringDiagnosticPolicy.Warning(TestId, Name, "No secret provider health probes are registered."));

        return Task.FromResult(EngineeringDiagnosticPolicy.Passed(
            TestId,
            Name,
            $"{providers.Count} secret provider health probe(s) are registered.",
            string.Join(", ", providers.Select(x => x.ProviderName).OrderBy(x => x, StringComparer.OrdinalIgnoreCase))));
    }
}

public sealed class CommonSecretsEnabledProviderDiagnosticLevelTest : IDiagnosticLevelLocalTest
{
    private readonly IReadOnlyList<ISecretProviderHealth> providers;

    public CommonSecretsEnabledProviderDiagnosticLevelTest(IEnumerable<ISecretProviderHealth> providers)
        => this.providers = (providers ?? throw new ArgumentNullException(nameof(providers))).ToArray();

    public string TestId => "COMMON.SECRETS.L5.PROVIDERS.ENABLED";
    public string Name => "At least one secret provider enabled";
    public string Owner => "Common.Secrets";
    public EngineeringDiagnosticLevel Level => EngineeringDiagnosticLevel.Level5Scan;
    public bool IsDestructive => false;

    public Task<EngineeringDiagnosticCheckResult> RunAsync(CancellationToken cancellationToken)
    {
        ISecretProviderHealth[] enabled = providers.Where(x => x.IsEnabled).ToArray();
        return Task.FromResult(enabled.Length > 0
            ? EngineeringDiagnosticPolicy.Passed(TestId, Name, $"{enabled.Length} secret provider(s) are enabled.", string.Join(", ", enabled.Select(x => x.ProviderName)))
            : EngineeringDiagnosticPolicy.Warning(TestId, Name, "No registered secret provider is enabled."));
    }
}

public sealed class CommonSecretsProviderAvailabilityDiagnosticLevelTest : IDiagnosticLevelLocalTest
{
    private readonly IReadOnlyList<ISecretProviderHealth> providers;

    public CommonSecretsProviderAvailabilityDiagnosticLevelTest(IEnumerable<ISecretProviderHealth> providers)
        => this.providers = (providers ?? throw new ArgumentNullException(nameof(providers))).ToArray();

    public string TestId => "COMMON.SECRETS.L4.PROVIDERS.AVAILABLE";
    public string Name => "Enabled secret providers available";
    public string Owner => "Common.Secrets";
    public EngineeringDiagnosticLevel Level => EngineeringDiagnosticLevel.Level4Analysis;
    public bool IsDestructive => false;

    public async Task<EngineeringDiagnosticCheckResult> RunAsync(CancellationToken cancellationToken)
    {
        List<string> unavailable = [];
        List<string> available = [];
        foreach (ISecretProviderHealth provider in providers.Where(x => x.IsEnabled))
        {
            cancellationToken.ThrowIfCancellationRequested();
            SecretProviderHealth health = await provider.CheckHealthAsync(cancellationToken).ConfigureAwait(false);
            if (health.IsAvailable) available.Add(provider.ProviderName);
            else unavailable.Add(provider.ProviderName);
        }

        if (unavailable.Count > 0)
            return EngineeringDiagnosticPolicy.Failed(
                TestId,
                Name,
                $"{unavailable.Count} enabled secret provider(s) are unavailable.",
                $"Unavailable={string.Join(",", unavailable)}; Available={string.Join(",", available)}");

        if (available.Count == 0)
            return EngineeringDiagnosticPolicy.Warning(TestId, Name, "No enabled secret provider was available to test.");

        return EngineeringDiagnosticPolicy.Passed(
            TestId,
            Name,
            "All enabled secret providers are available.",
            string.Join(", ", available));
    }
}

public sealed class CommonSecretsIdentityCompletenessDiagnosticLevelTest : IDiagnosticLevelLocalTest
{
    private readonly CommonSecretsOptions options;

    public CommonSecretsIdentityCompletenessDiagnosticLevelTest(CommonSecretsOptions options)
        => this.options = options ?? throw new ArgumentNullException(nameof(options));

    public string TestId => "COMMON.SECRETS.L4.IDENTITY.COMPLETE";
    public string Name => "Secrets workload identity configuration complete";
    public string Owner => "Common.Secrets";
    public EngineeringDiagnosticLevel Level => EngineeringDiagnosticLevel.Level4Analysis;
    public bool IsDestructive => false;

    public Task<EngineeringDiagnosticCheckResult> RunAsync(CancellationToken cancellationToken)
    {
        List<string> missing = [];
        if (string.IsNullOrWhiteSpace(options.WorkloadName)) missing.Add(nameof(options.WorkloadName));
        if (string.IsNullOrWhiteSpace(options.EnvironmentName)) missing.Add(nameof(options.EnvironmentName));
        if (string.IsNullOrWhiteSpace(options.MachineName)) missing.Add(nameof(options.MachineName));

        return Task.FromResult(missing.Count == 0
            ? EngineeringDiagnosticPolicy.Passed(TestId, Name, "Secrets workload identity metadata is complete.")
            : EngineeringDiagnosticPolicy.Warning(TestId, Name, "Secrets workload identity metadata is incomplete.", $"Missing={string.Join(",", missing)}"));
    }
}
