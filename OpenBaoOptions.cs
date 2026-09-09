namespace Common.Secrets;

public sealed class OpenBaoOptions
{
    public bool Enabled { get; init; }

    public string Address { get; init; } = string.Empty;

    public string MountPath { get; init; } = "secret";

    public string BasePath { get; init; } = "aegis";

    public string AuthMountPath { get; init; } = "approle";

    public string RoleId { get; init; } = string.Empty;

    public string SecretIdEnvironmentVariable { get; init; } = "OPENBAO_SECRET_ID";

    public string? DevelopmentToken { get; init; }

    public string DevelopmentTokenEnvironmentVariable { get; init; } = "OPENBAO_TOKEN";

    public bool RequireHttps { get; init; } = true;
}
