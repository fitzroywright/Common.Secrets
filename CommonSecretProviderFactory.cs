using Microsoft.Extensions.Configuration;

namespace Common.Secrets;

public static class CommonSecretProviderFactory
{
    public static ISecretProvider Create(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        CommonSecretsOptions commonOptions = configuration
            .GetSection("CommonSecrets")
            .Get<CommonSecretsOptions>() ?? new CommonSecretsOptions();

        return CreateProviderSet(
            configuration,
            commonOptions,
            new NullSecretTelemetrySink()).Provider;
    }

    internal static ConfiguredSecretProviderSet CreateProviderSet(
        IConfiguration configuration,
        CommonSecretsOptions commonOptions,
        ISecretTelemetrySink telemetry)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(commonOptions);
        ArgumentNullException.ThrowIfNull(telemetry);

        IReadOnlyDictionary<string, SecretProviderDefinition> definitions =
            SecretProviderConfiguration.ReadProviders(configuration);

        CommonSecretsPolicy.Validate(commonOptions, definitions);

        string[] providerOrder =
            CommonSecretsPolicy.ResolveProviderOrder(commonOptions, definitions);

        List<ISecretProvider> orderedProviders = [];
        Dictionary<string, ISecretProviderHealth> healthProviders =
            new(StringComparer.OrdinalIgnoreCase);

        foreach (string providerName in providerOrder)
        {
            SecretProviderDefinition definition = definitions[providerName];
            ISecretProvider concrete = CreateConcreteProvider(
                definition,
                configuration,
                commonOptions);

            ISecretProvider named =
                new NamedSecretProvider(providerName, concrete);

            ObservedSecretProvider observed =
                new(named, telemetry, commonOptions);

            orderedProviders.Add(observed);
            healthProviders.Add(providerName, observed);
        }

        return new ConfiguredSecretProviderSet(
            new ChainedSecretProvider(orderedProviders),
            providerOrder,
            healthProviders);
    }

    private static ISecretProvider CreateConcreteProvider(
        SecretProviderDefinition definition,
        IConfiguration configuration,
        CommonSecretsOptions commonOptions)
    {
        switch (definition.Type.Trim().ToUpperInvariant())
        {
            case "ENVIRONMENT":
                return new EnvironmentSecretProvider();

            case "CONFIGURATION":
                return new ConfigurationSecretProvider(configuration);

            case "BITWARDENPASSWORDMANAGER":
            {
                BitwardenPasswordManagerOptions options =
                    definition.Settings.Get<BitwardenPasswordManagerOptions>()
                    ?? new BitwardenPasswordManagerOptions();

                return new BitwardenPasswordManagerSecretProvider(options);
            }

            case "BITWARDENSECRETSMANAGER":
            {
                BitwardenSecretsManagerOptions options =
                    definition.Settings.Get<BitwardenSecretsManagerOptions>()
                    ?? new BitwardenSecretsManagerOptions();

                return new BitwardenSecretProvider(options);
            }

            case "OPENBAO":
            {
                OpenBaoOptions options =
                    definition.Settings.Get<OpenBaoOptions>()
                    ?? new OpenBaoOptions();

                HttpClient client = new();
                IOpenBaoAuthenticator authenticator =
                    new OpenBaoAuthenticator(options, commonOptions, client);

                return new OpenBaoSecretProvider(options, client, authenticator);
            }

            default:
                throw new InvalidOperationException(
                    $"Common.Secrets provider instance '{definition.Name}' declares unsupported type '{definition.Type}'.");
        }
    }
}

internal sealed class ConfiguredSecretProviderSet
{
    public ConfiguredSecretProviderSet(
        ISecretProvider provider,
        IReadOnlyList<string> order,
        IReadOnlyDictionary<string, ISecretProviderHealth> healthProviders)
    {
        Provider = provider ?? throw new ArgumentNullException(nameof(provider));
        Order = order ?? throw new ArgumentNullException(nameof(order));
        HealthProviders = healthProviders ?? throw new ArgumentNullException(nameof(healthProviders));
    }

    public ISecretProvider Provider { get; }

    public IReadOnlyList<string> Order { get; }

    public IReadOnlyDictionary<string, ISecretProviderHealth> HealthProviders { get; }
}
