using Microsoft.Extensions.Configuration;

namespace Common.Secrets;

/// <summary>
/// One configured Common.Secrets provider instance.
/// The provider type owns the schema beneath Settings.
/// </summary>
public sealed record SecretProviderDefinition(
    string Name,
    string Type,
    IConfigurationSection Settings);

public static class SecretProviderConfiguration
{
    public static IReadOnlyDictionary<string, SecretProviderDefinition> ReadProviders(
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        Dictionary<string, SecretProviderDefinition> providers =
            new(StringComparer.OrdinalIgnoreCase);

        foreach (IConfigurationSection providerSection in configuration
                     .GetSection("CommonSecrets:Providers")
                     .GetChildren())
        {
            string name = providerSection.Key?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(name))
                throw new InvalidOperationException("Common.Secrets provider instance names cannot be blank.");

            string type = providerSection["Type"]?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(type))
                throw new InvalidOperationException(
                    $"Common.Secrets provider instance '{name}' must declare a Type.");

            if (!providers.TryAdd(
                    name,
                    new SecretProviderDefinition(
                        name,
                        type,
                        providerSection.GetSection("Settings"))))
            {
                throw new InvalidOperationException(
                    $"Common.Secrets provider instance '{name}' is declared more than once.");
            }
        }

        return providers;
    }
}
