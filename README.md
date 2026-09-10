# Common.Secrets

Reusable secret retrieval, provider composition, and OpenBao integration for the application family.

## Repository policy

`main` is the authoritative trunk and the only branch that should be used for ongoing development, integration, packaging, and releases. Older development, hardening, integration, and live-test branches are historical once their work has been incorporated into `main`.

`Common.Secrets` deliberately contains no application-specific secret names. Consumers own their secret names and provider precedence; this package owns retrieval, provider composition, policy enforcement, provider observation/telemetry, and reusable secret-store integration behavior.

## Core API

```csharp
ISecretProvider.GetAsync(name)
ISecretProvider.GetRequiredAsync(name)
```

## Configurable provider order

Every consuming application can choose its own provider precedence through `CommonSecrets:ProviderOrder`.

The default order is:

1. Environment
2. Bitwarden Password Manager
3. Bitwarden Secrets Manager
4. OpenBao
5. .NET Configuration

Example:

```json
{
  "CommonSecrets": {
    "ProviderOrder": [
      "Environment",
      "BitwardenPasswordManager",
      "BitwardenSecretsManager",
      "OpenBao",
      "Configuration"
    ]
  }
}
```

An application can use a different order or omit providers entirely. A server application can prefer Environment/OpenBao/Configuration while a developer workstation can prefer Environment/Bitwarden Password Manager/Configuration.

The first provider returning a non-empty secret wins. Provider names are case-insensitive. `Bitwarden` remains supported as a legacy alias for `BitwardenSecretsManager`, but the canonical provider name is `BitwardenSecretsManager`. A provider may appear only once in the configured order.

## OpenBao

OpenBao support uses the HTTP API and the KV v2 secret engine. A secret requested as:

```text
Database:Password
```

is read by default from:

```text
/v1/secret/data/aegis/Database/Password
```

with the secret value stored in the KV document's `value` field.

Example application configuration:

```json
{
  "CommonSecrets": {
    "OpenBao": {
      "Enabled": true,
      "Address": "https://openbao.internal.example:8200",
      "MountPath": "secret",
      "BasePath": "aegis/studio",
      "RequireHttps": true
    }
  }
}
```

Bootstrap credentials must remain outside source control. `OPENBAO_TOKEN` can be supplied through the process/container environment when token bootstrap is appropriate. `TokenEnvironmentVariable` can be changed if a host needs a different environment-variable name. A `Token` configuration value exists for controlled bootstrap scenarios but must not be committed to source control.

OpenBao integration includes authentication/bootstrap behavior, reauthentication support, lease-lifecycle hardening, provider observation/telemetry, and integration with `Common.Diagnostics`. Diagnostic and telemetry output must never expose retrieved secret values, tokens, passwords, or equivalent secret material.

For an internal OpenBao deployment, expose the API only to required application hosts, use TLS in production, and issue application-specific authentication/policies restricted to the application's required secret paths.

## Bitwarden

`BitwardenPasswordManager` and `BitwardenSecretsManager` are separate providers so applications can choose the appropriate trust model and order independently.

Password Manager is most suitable for interactive/developer scenarios. Secrets Manager and OpenBao are better suited to unattended services.

## Failure and recovery expectations

Common.Secrets should fail safely when a provider is unavailable, surface useful provider/diagnostic telemetry without exposing secret material, and permit recovery/reauthentication when the provider becomes available again. Production validation should explicitly prove the sequence: successful retrieval -> provider outage -> controlled failure -> provider recovery -> reauthentication -> successful retrieval without requiring an application restart where the provider supports that lifecycle.

## Current maturity

Provider composition, OpenBao integration, Secret Zero hardening, lease lifecycle, reauthentication, telemetry, diagnostics integration, and automated tests are implemented on `main`. Remaining work is primarily operational proof against real OpenBao and real consuming applications, including outage/recovery/reauthentication and production credential lifecycle behavior.

## Development

```powershell
dotnet restore
dotnet build
dotnet test
```

## Package

Package ID: `Common.Secrets`
