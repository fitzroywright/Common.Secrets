namespace Common.Secrets.Tests;

public sealed class BitwardenSecretProviderTests
{
    [Fact]
    public async Task GetAsync_ReturnsNull_WhenProviderDisabled()
    {
        BitwardenSecretProvider provider = new(new BitwardenSecretsManagerOptions
        {
            Enabled = false,
            SecretIds = new Dictionary<string, string>
            {
                ["X"] = Guid.NewGuid().ToString()
            }
        });

        string? value = await provider.GetAsync("X");

        Assert.Null(value);
    }

    [Fact]
    public async Task GetAsync_ReturnsNull_WhenSecretIdIsNotConfigured()
    {
        BitwardenSecretProvider provider = new(new BitwardenSecretsManagerOptions
        {
            Enabled = true
        });

        string? value = await provider.GetAsync("Missing");

        Assert.Null(value);
    }

    [Fact]
    public async Task GetAsync_RejectsInvalidSecretIdBeforeStartingCli()
    {
        BitwardenSecretProvider provider = new(new BitwardenSecretsManagerOptions
        {
            Enabled = true,
            SecretIds = new Dictionary<string, string>
            {
                ["X"] = "not-a-guid"
            }
        });

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() => provider.GetAsync("X"));

        Assert.Contains("not a valid UUID", exception.Message);
    }

    [Fact]
    public async Task GetAsync_RequiresConfiguredBootstrapToken()
    {
        const string environmentVariable = "COMMON_SECRETS_TEST_MISSING_BWS_TOKEN";
        Environment.SetEnvironmentVariable(environmentVariable, null);

        try
        {
            BitwardenSecretProvider provider = new(new BitwardenSecretsManagerOptions
            {
                Enabled = true,
                AccessTokenEnvironmentVariable = environmentVariable,
                SecretIds = new Dictionary<string, string>
                {
                    ["X"] = Guid.NewGuid().ToString()
                }
            });

            InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() => provider.GetAsync("X"));

            Assert.Contains(environmentVariable, exception.Message);
        }
        finally
        {
            Environment.SetEnvironmentVariable(environmentVariable, null);
        }
    }
}
