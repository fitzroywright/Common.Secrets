namespace Common.Secrets;

public sealed class OpenBaoOptions
{
    public bool Enabled { get; init; }

    public string Address { get; init; } = string.Empty;

    public string MountPath { get; init; } = "secret";

    public string BasePath { get; init; } = "aegis";

    public string? Token { get; init; }

    public string TokenEnvironmentVariable { get; init; } = "OPENBAO_TOKEN";

    public bool RequireHttps { get; init; } = true;
}
