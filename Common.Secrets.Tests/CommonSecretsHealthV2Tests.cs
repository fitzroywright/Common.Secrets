using Xunit;

namespace Common.Secrets.Tests;

public sealed class CommonSecretsHealthV2Tests
{
    [Fact]
    public async Task Missing_required_secret_names_are_reported_without_values()
    {
        var provider = new FakeSecretProvider(new Dictionary<string,string?>
        {
            ["Present"] = "super-secret-value",
            ["Missing"] = null
        });
        var health = new FakeHealthCheck(new CommonSecretsHealth(true, true, "Fake", "Available."));

        CommonSecretsHealthV2 result = await CommonSecretsHealthEvaluator.EvaluateAsync(
            provider,
            health,
            ["Present", "Missing"]);

        Assert.Equal(["Missing"], result.MissingRequiredSecrets);
        Assert.True(result.IsReachable);
        Assert.DoesNotContain("super-secret-value", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Authentication_failure_is_distinct_from_reachability_unknown()
    {
        var provider = new FakeSecretProvider([]);
        var authFailure = new FakeHealthCheck(new CommonSecretsHealth(
            true,
            false,
            "Fake",
            "Authentication credential was rejected."));

        CommonSecretsHealthV2 result = await CommonSecretsHealthEvaluator.EvaluateAsync(
            provider,
            authFailure);

        Assert.Equal(
            SecretProviderAuthenticationState.AuthenticationFailed,
            result.AuthenticationState);
        Assert.False(result.IsReady);
    }

    [Fact]
    public async Task Unreachable_provider_without_auth_evidence_remains_auth_unknown()
    {
        var provider = new FakeSecretProvider([]);
        var unavailable = new FakeHealthCheck(new CommonSecretsHealth(
            true,
            false,
            "Fake",
            "Provider endpoint is unreachable."));

        CommonSecretsHealthV2 result = await CommonSecretsHealthEvaluator.EvaluateAsync(
            provider,
            unavailable);

        Assert.Equal(
            SecretProviderAuthenticationState.Unknown,
            result.AuthenticationState);
        Assert.False(result.IsReachable);
    }

    private sealed class FakeSecretProvider(IReadOnlyDictionary<string,string?> values) : ISecretProvider
    {
        public Task<string?> GetAsync(string name, CancellationToken cancellationToken = default) =>
            Task.FromResult(values.TryGetValue(name, out string? value) ? value : null);

        public async Task<string> GetRequiredAsync(string name, CancellationToken cancellationToken = default) =>
            await GetAsync(name, cancellationToken)
            ?? throw new InvalidOperationException("Required secret is unavailable.");
    }

    private sealed class FakeHealthCheck(CommonSecretsHealth health) : ICommonSecretsHealthCheck
    {
        public Task<CommonSecretsHealth> CheckAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(health);
    }
}
