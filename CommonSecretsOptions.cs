namespace Common.Secrets;

public enum SecretsEnvironmentMode
{
    Production = 0,
    Development = 1,
    OfflineDevelopment = 2,
    Test = 3
}

public sealed class CommonSecretsOptions
{
    public SecretsEnvironmentMode Mode { get; init; } = SecretsEnvironmentMode.Production;

    public string[]? ProviderOrder { get; init; }

    public string WorkloadName { get; init; } = string.Empty;

    public string EnvironmentName { get; init; } = string.Empty;

    public string MachineName { get; init; } = string.Empty;
}
