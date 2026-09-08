namespace Common.Secrets;

public sealed class CommonSecretsOptions
{
    public string[] ProviderOrder { get; init; } =
    [
        "Environment",
        "OpenBao",
        "Bitwarden",
        "BitwardenPasswordManager",
        "Configuration"
    ];
}
