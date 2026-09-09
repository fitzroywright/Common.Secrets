namespace Common.Secrets;

public sealed class CommonSecretsOptions
{
    public string[] ProviderOrder { get; init; } =
    [
        "Environment",
        "OpenBao",
        "Configuration"
    ];

    public string WorkloadName { get; init; } = string.Empty;

    public string EnvironmentName { get; init; } = string.Empty;

    public string MachineName { get; init; } = string.Empty;
}
