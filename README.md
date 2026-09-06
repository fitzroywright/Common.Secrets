# Common.Secrets

Reusable secret retrieval for Aegis applications.

`Common.Secrets` deliberately contains no Aegis Studio, messaging, Active Directory, Bitwarden, or OpenBao-specific secret names. Consumers own their secret names; this package owns retrieval and provider composition.

## Core API

```csharp
ISecretProvider.GetAsync(name)
ISecretProvider.GetRequiredAsync(name)
```

The default provider chain is:

1. environment variables
2. OpenBao KV v2, when enabled
3. .NET configuration

This allows local or container environment variables to override centrally managed secrets while keeping normal configuration as a development fallback.

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

## Development

```powershell
dotnet restore
dotnet build
dotnet test
```

## Package

Package ID: `Common.Secrets`
