namespace Common.Secrets;

public sealed class BitwardenSecretsManagerOptions
{
    public bool Enabled { get; init; }
    public string CliPath { get; init; } = "bws";
    public string AccessTokenEnvironmentVariable { get; init; } = "BWS_ACCESS_TOKEN";
    public string? ServerUrl { get; init; }
    public TimeSpan CommandTimeout { get; init; } = TimeSpan.FromSeconds(15);
    public Dictionary<string, string> SecretIds { get; init; }
        = new(StringComparer.OrdinalIgnoreCase);
}
