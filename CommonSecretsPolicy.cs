using Microsoft.Extensions.Configuration;

namespace Common.Secrets;

public static class CommonSecretsPolicy
{
    private static readonly IReadOnlyDictionary<SecretsEnvironmentMode, HashSet<string>> AllowedProviderTypes =
        new Dictionary<SecretsEnvironmentMode, HashSet<string>>
        {
            [SecretsEnvironmentMode.Production] =
                new(StringComparer.OrdinalIgnoreCase) { "OpenBao" },

            [SecretsEnvironmentMode.Development] =
                new(StringComparer.OrdinalIgnoreCase)
                {
                    "Environment",
                    "OpenBao",
                    "BitwardenPasswordManager",
                    "BitwardenSecretsManager",
                    "Configuration"
                },

            [SecretsEnvironmentMode.OfflineDevelopment] =
                new(StringComparer.OrdinalIgnoreCase)
                {
                    "OpenBao",
                    "Environment",
                    "Configuration"
                },

            [SecretsEnvironmentMode.Test] =
                new(StringComparer.OrdinalIgnoreCase)
                {
                    "Environment",
                    "Configuration"
                }
        };

    public static string[] ResolveProviderOrder(
        CommonSecretsOptions options,
        IReadOnlyDictionary<string, SecretProviderDefinition> providers)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(providers);

        if (providers.Count == 0)
        {
            throw new InvalidOperationException(
                "Common.Secrets requires at least one provider instance under CommonSecrets:Providers.");
        }

        string[] order;
        if (options.ProviderOrder is { Length: > 0 })
        {
            order = options.ProviderOrder
                .Select(x => x?.Trim() ?? string.Empty)
                .ToArray();
        }
        else if (providers.Count == 1)
        {
            order = [providers.Keys.Single()];
        }
        else
        {
            throw new InvalidOperationException(
                "CommonSecrets:ProviderOrder is required when more than one provider instance is configured.");
        }

        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> allowedTypes = AllowedProviderTypes[options.Mode];

        foreach (string providerName in order)
        {
            if (string.IsNullOrWhiteSpace(providerName))
                throw new InvalidOperationException("Common.Secrets provider order cannot contain blank names.");

            if (!providers.TryGetValue(providerName, out SecretProviderDefinition? definition))
            {
                throw new InvalidOperationException(
                    $"Common.Secrets provider order references unknown instance '{providerName}'.");
            }

            if (!allowedTypes.Contains(definition.Type))
            {
                throw new InvalidOperationException(
                    $"Provider instance '{providerName}' uses type '{definition.Type}', which is not allowed in Common.Secrets mode '{options.Mode}'.");
            }

            if (!seen.Add(providerName))
            {
                throw new InvalidOperationException(
                    $"Provider instance '{providerName}' appears more than once in CommonSecrets:ProviderOrder.");
            }
        }

        return order;
    }

    public static void Validate(
        CommonSecretsOptions options,
        IReadOnlyDictionary<string, SecretProviderDefinition> providers)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(providers);

        string[] order = ResolveProviderOrder(options, providers);

        foreach (string providerName in order)
        {
            SecretProviderDefinition definition = providers[providerName];

            if (string.Equals(definition.Type, "OpenBao", StringComparison.OrdinalIgnoreCase))
            {
                OpenBaoOptions openBaoOptions =
                    definition.Settings.Get<OpenBaoOptions>() ?? new OpenBaoOptions();

                if (options.Mode == SecretsEnvironmentMode.Production)
                {
                    if (!openBaoOptions.Enabled)
                    {
                        throw new InvalidOperationException(
                            $"Production provider instance '{providerName}' must enable OpenBao.");
                    }

                    if (!openBaoOptions.RequireHttps)
                    {
                        throw new InvalidOperationException(
                            $"Production provider instance '{providerName}' must require HTTPS.");
                    }
                }

                if (options.Mode == SecretsEnvironmentMode.OfflineDevelopment &&
                    openBaoOptions.Enabled)
                {
                    if (!Uri.TryCreate(openBaoOptions.Address, UriKind.Absolute, out Uri? uri) ||
                        !uri.IsLoopback)
                    {
                        throw new InvalidOperationException(
                            $"OfflineDevelopment provider instance '{providerName}' requires a loopback/local-only OpenBao address.");
                    }
                }
            }
        }
    }
}
