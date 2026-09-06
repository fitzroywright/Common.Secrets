# Common.Secrets

Reusable secret retrieval for Aegis applications.

`Common.Secrets` deliberately contains no Aegis Studio, messaging, Active Directory, Bitwarden, or OpenBao-specific secret names. Consumers own their secret names; this package owns retrieval and provider composition.

## Core API

```csharp
ISecretProvider.GetAsync(name)
ISecretProvider.GetRequiredAsync(name)
```

The default provider chain checks environment variables first, then .NET configuration. Additional providers such as Bitwarden or OpenBao can be added later without changing consuming applications.

## Development

```powershell
dotnet restore
dotnet build
dotnet test
```

## Package

Package ID: `Common.Secrets`
