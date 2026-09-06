namespace Common.Secrets.Tests;

public sealed class ChainedSecretProviderTests
{
    [Fact]
    public async Task GetAsyncReturnsFirstNonEmptyValue()
    {
        ISecretProvider first = new StubSecretProvider(null);
        ISecretProvider second = new StubSecretProvider("secret-value");
        ChainedSecretProvider provider = new(new[] { first, second });

        string? value = await provider.GetAsync("Test:Secret");

        Assert.Equal("secret-value", value);
    }

    [Fact]
    public async Task GetRequiredAsyncThrowsWhenSecretIsMissing()
    {
        ChainedSecretProvider provider = new(new[] { new StubSecretProvider(null) });

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.GetRequiredAsync("Missing:Secret"));
    }

    private sealed class StubSecretProvider : SecretProviderBase
    {
        private readonly string? value;

        public StubSecretProvider(string? value)
        {
            this.value = value;
        }

        public override Task<string?> GetAsync(
            string name,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(value);
        }
    }
}
