using Microsoft.Extensions.Configuration;

namespace Common.Secrets;

/// <summary>
/// Safe, provider-neutral metadata describing the configured Common.Secrets capability.
/// Consumers may observe this metadata without knowing provider-specific configuration schemas.
/// </summary>
public sealed record SecretProviderMetadata(
    SecretsEnvironmentMode Mode,
    IReadOnlyList<string> ProviderOrder,
    string ActiveProvider,
    string? ManagementUrl = null);

public static class SecretProviderMetadataResolver
{
    public static SecretProviderMetadata Resolve(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        CommonSecretsOptions commonOptions = configuration
            .GetSection("CommonSecrets")
            .Get<CommonSecretsOptions>() ?? new CommonSecretsOptions();

        string[] providerOrder = CommonSecretsPolicy.ResolveProviderOrder(commonOptions);
        string activeProvider = providerOrder.FirstOrDefault() ?? "None";
        string? managementUrl = ResolveManagementUrl(configuration, activeProvider);

        return new SecretProviderMetadata(
            commonOptions.Mode,
            providerOrder,
            activeProvider,
            managementUrl);
    }

    private static string? ResolveManagementUrl(IConfiguration configuration, string provider)
    {
        // Provider-specific configuration is interpreted here, inside Common.Secrets.
        if (provider.Equals("OpenBao", StringComparison.OrdinalIgnoreCase))
            return NullIfBlank(configuration["CommonSecrets:OpenBao:Address"]);

        return null;
    }

    private static string? NullIfBlank(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
