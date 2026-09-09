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
    public string? SecretIdFile { get; init; }
    public bool DeleteSecretIdFileAfterRead { get; init; } = true;
    public bool RequireSecretIdFileInProduction { get; init; }
    public string? DevelopmentToken { get; init; }
    public string DevelopmentTokenEnvironmentVariable { get; init; } = "OPENBAO_TOKEN";
    public bool RequireHttps { get; init; } = true;
    public bool RenewTokens { get; init; } = true;
    public bool RevokeTokenOnDispose { get; init; } = true;
    public TimeSpan TokenRenewalSafetyWindow { get; init; } = TimeSpan.FromSeconds(30);
    public TimeSpan SecretCacheTtl { get; init; } = TimeSpan.FromMinutes(5);
}
