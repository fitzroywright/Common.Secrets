# Common.Secrets

Reusable secret retrieval for Aegis applications.

`Common.Secrets` deliberately contains no application-specific secret names. Consumers own their secret names and provider precedence; this package owns retrieval and provider composition.

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

An application can use a different order or omit providers entirely. For example, a server application can prefer OpenBao:

```json
{
  "CommonSecrets": {
    "ProviderOrder": [
      "Environment",
      "OpenBao",
      "Configuration"
    ]
  }
}
```

A developer workstation can instead prefer Bitwarden Password Manager:

```json
{
  "CommonSecrets": {
    "ProviderOrder": [
      "Environment",
      "BitwardenPasswordManager",
      "Configuration"
    ]
  }
}
```

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

Provide the OpenBao token outside source control, preferably through the process/container environment:

```text
OPENBAO_TOKEN=<token>
```

`TokenEnvironmentVariable` can be changed if a host needs a different environment-variable name. A `Token` configuration value is also supported for bootstrap scenarios, but should not be committed to source control.

For an internal OpenBao instance running on the Bitwarden VM or in a separate Docker container on that VM, expose the OpenBao API only to the required internal application hosts, use TLS in production, and issue an application-specific token/policy restricted to that application's secret path.

## Bitwarden

`BitwardenPasswordManager` and `BitwardenSecretsManager` are separate providers so applications can choose the appropriate trust model and order independently.

Password Manager is most suitable for interactive/developer scenarios. Secrets Manager and OpenBao are better suited to unattended services.

## Development

```powershell
dotnet restore
dotnet build
dotnet test
```

## Package

Package ID: `Common.Secrets`
