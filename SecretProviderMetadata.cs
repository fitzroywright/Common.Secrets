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

        IReadOnlyDictionary<string, SecretProviderDefinition> providers =
            SecretProviderConfiguration.ReadProviders(configuration);

        string[] providerOrder =
            CommonSecretsPolicy.ResolveProviderOrder(commonOptions, providers);

        string activeProvider =
            providerOrder.FirstOrDefault() ?? "None";

        string? managementUrl =
            ResolveManagementUrl(providers, activeProvider);

        return new SecretProviderMetadata(
            commonOptions.Mode,
            providerOrder,
            activeProvider,
            managementUrl);
    }

    private static string? ResolveManagementUrl(
        IReadOnlyDictionary<string, SecretProviderDefinition> providers,
        string providerName)
    {
        if (!providers.TryGetValue(providerName, out SecretProviderDefinition? definition))
            return null;

        // Provider-specific settings remain interpreted inside Common.Secrets.
        if (definition.Type.Equals("OpenBao", StringComparison.OrdinalIgnoreCase))
            return NullIfBlank(definition.Settings["Address"]);

        if (definition.Type.Equals("BitwardenPasswordManager", StringComparison.OrdinalIgnoreCase))
            return NullIfBlank(definition.Settings["BaseUrl"]);

        if (definition.Type.Equals("BitwardenSecretsManager", StringComparison.OrdinalIgnoreCase))
            return NullIfBlank(definition.Settings["ServerUrl"]);

        return null;
    }

    private static string? NullIfBlank(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
