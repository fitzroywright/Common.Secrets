# Common.Secrets

Reusable secret retrieval and provider composition for the Aegis application family.

## Repository policy

`main` is the authoritative trunk and the only branch used for ongoing development,
integration, packaging, and releases.

Common.Secrets deliberately contains no application-specific secret names. Consumers
own their secret names and provider precedence. The library owns retrieval, provider
composition, policy enforcement, provider observation/telemetry, and reusable provider
implementations.

## Core API

```csharp
ISecretProvider.GetAsync(name)
ISecretProvider.GetRequiredAsync(name)
```

## Provider-instance configuration contract

Common.Secrets uses one provider-neutral configuration structure. There is no legacy
`CommonSecrets:OpenBao`, `CommonSecrets:Bitwarden`, or provider alias parser.

Each provider instance has:

- an application-selected instance `Name`;
- a provider `Type`;
- a provider-owned `Settings` payload.

`ProviderOrder` references provider instance names, not provider types.

```json
{
  "CommonSecrets": {
    "Mode": "Production",
    "ProviderOrder": [
      "PrimarySecrets"
    ],
    "Providers": {
      "PrimarySecrets": {
        "Type": "OpenBao",
        "Settings": {
          "Enabled": true,
          "Address": "https://openbao.internal.example:8200",
          "MountPath": "secret",
          "BasePath": "aegis/studio",
          "RequireHttps": true
        }
      }
    }
  }
}
```

A development application can define multiple named instances and order them
independently:

```json
{
  "CommonSecrets": {
    "Mode": "Development",
    "ProviderOrder": [
      "ProcessEnvironment",
      "DeveloperVault",
      "LocalConfiguration"
    ],
    "Providers": {
      "ProcessEnvironment": {
        "Type": "Environment",
        "Settings": {}
      },
      "DeveloperVault": {
        "Type": "BitwardenPasswordManager",
        "Settings": {
          "Enabled": true,
          "BaseUrl": "http://127.0.0.1:8087/"
        }
      },
      "LocalConfiguration": {
        "Type": "Configuration",
        "Settings": {}
      }
    }
  }
}
```

If exactly one provider instance is configured, `ProviderOrder` may be omitted.
With multiple providers, the order must be explicit.

The first provider returning a non-empty secret wins.

## Supported provider types

- `Environment`
- `Configuration`
- `OpenBao`
- `BitwardenPasswordManager`
- `BitwardenSecretsManager`

Provider instance names are arbitrary and may be deployment-specific. Multiple
instances of the same provider type are supported.

## Secret Zero

Bootstrap secret material remains outside the provider settings payload.

Provider settings may identify the external bootstrap mechanism (for example the
name of an environment variable or protected file), but the bootstrap credential
itself must not be committed to application configuration or source control.

For OpenBao AppRole bootstrap, the Secret ID remains external through the configured
environment-variable or protected-file mechanism. Common.Secrets consumes and, where
configured, clears/deletes that bootstrap material after use.

## Policy

Production mode fails closed and permits only OpenBao provider instances. Production
OpenBao must be enabled and must require HTTPS.

Development permits Environment, Configuration, OpenBao, Bitwarden Password Manager,
and Bitwarden Secrets Manager.

OfflineDevelopment permits only local/loopback OpenBao plus Environment and
Configuration.

Test mode permits Environment and Configuration.

## Failure and recovery expectations

Common.Secrets should fail safely when a provider is unavailable, surface useful
provider-neutral telemetry without exposing secret material, and permit recovery or
reauthentication when the provider becomes available again.

Observability must never be allowed to fail secret retrieval solely because a
telemetry sink is unavailable.

## Development

```powershell
dotnet restore
dotnet build
dotnet test
```

## Package

Package ID: `Common.Secrets`
