namespace Common.Secrets;

public sealed class EnvironmentSecretProvider : SecretProviderBase
{
    public override Task<string?> GetAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        cancellationToken.ThrowIfCancellationRequested();

        string environmentName = name.Replace(':', '_').Replace("__", "_");
        string? value = Environment.GetEnvironmentVariable(name)
            ?? Environment.GetEnvironmentVariable(name.Replace(":", "__"))
            ?? Environment.GetEnvironmentVariable(environmentName);

        return Task.FromResult(value);
    }
}
