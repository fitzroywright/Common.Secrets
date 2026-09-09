namespace Common.Secrets;

public static class CommonSecretsPolicy
{
    private static readonly IReadOnlyDictionary<SecretsEnvironmentMode, string[]> DefaultProviderOrder =
        new Dictionary<SecretsEnvironmentMode, string[]>
        {
            [SecretsEnvironmentMode.Production] = ["OpenBao"],
            [SecretsEnvironmentMode.Development] = ["Environment", "OpenBao", "Bitwarden", "Configuration"],
            [SecretsEnvironmentMode.OfflineDevelopment] = ["OpenBao", "Environment", "Configuration"],
            [SecretsEnvironmentMode.Test] = ["Environment", "Configuration"]
        };

    private static readonly IReadOnlyDictionary<SecretsEnvironmentMode, HashSet<string>> AllowedProviders =
        new Dictionary<SecretsEnvironmentMode, HashSet<string>>
        {
            [SecretsEnvironmentMode.Production] = new(StringComparer.OrdinalIgnoreCase) { "OpenBao" },
            [SecretsEnvironmentMode.Development] = new(StringComparer.OrdinalIgnoreCase) { "Environment", "OpenBao", "Bitwarden", "Configuration" },
            [SecretsEnvironmentMode.OfflineDevelopment] = new(StringComparer.OrdinalIgnoreCase) { "OpenBao", "Environment", "Configuration" },
            [SecretsEnvironmentMode.Test] = new(StringComparer.OrdinalIgnoreCase) { "Environment", "Configuration" }
        };

    public static string[] ResolveProviderOrder(CommonSecretsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        string[] providers = options.ProviderOrder is { Length: > 0 }
            ? options.ProviderOrder
            : DefaultProviderOrder[options.Mode];

        ValidateProviderOrder(options.Mode, providers);
        return providers;
    }

    public static void Validate(
        CommonSecretsOptions options,
        OpenBaoOptions openBaoOptions)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(openBaoOptions);

        _ = ResolveProviderOrder(options);

        if (options.Mode == SecretsEnvironmentMode.Production)
        {
            if (!openBaoOptions.Enabled)
            {
                throw new InvalidOperationException("Common.Secrets production mode requires OpenBao to be enabled.");
            }

            if (!openBaoOptions.RequireHttps)
            {
                throw new InvalidOperationException("Common.Secrets production mode requires HTTPS for OpenBao.");
            }
        }

        if (options.Mode == SecretsEnvironmentMode.OfflineDevelopment && openBaoOptions.Enabled)
        {
            if (!Uri.TryCreate(openBaoOptions.Address, UriKind.Absolute, out Uri? uri) || !uri.IsLoopback)
            {
                throw new InvalidOperationException(
                    "Common.Secrets OfflineDevelopment mode requires the OpenBao address to be loopback/local-only.");
            }
        }
    }

    private static void ValidateProviderOrder(
        SecretsEnvironmentMode mode,
        IEnumerable<string> providerOrder)
    {
        HashSet<string> allowed = AllowedProviders[mode];
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);

        foreach (string providerName in providerOrder)
        {
            if (string.IsNullOrWhiteSpace(providerName))
            {
                throw new InvalidOperationException("Common.Secrets provider names cannot be blank.");
            }

            string normalized = providerName.Trim();
            if (!allowed.Contains(normalized))
            {
                throw new InvalidOperationException(
                    $"Provider '{normalized}' is not allowed in Common.Secrets mode '{mode}'.");
            }

            if (!seen.Add(normalized))
            {
                throw new InvalidOperationException(
                    $"Provider '{normalized}' appears more than once in Common.Secrets provider order.");
            }
        }

        if (mode == SecretsEnvironmentMode.Production && !seen.Contains("OpenBao"))
        {
            throw new InvalidOperationException("Common.Secrets production mode must use OpenBao and fails closed without it.");
        }
    }
}
