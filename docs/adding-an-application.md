# Adding an application to Common.Secrets

Applications consume logical secret names. They must not know which provider contains the value.

## 1. Register Common.Secrets

```csharp
services.AddCommonSecrets(configuration);
```

Set non-secret workload context in configuration:

```json
{
  "CommonSecrets": {
    "WorkloadName": "Cafeteria",
    "EnvironmentName": "Production",
    "ProviderOrder": [ "OpenBao" ]
  }
}
```

`MachineName` is optional. When omitted, Common.Secrets uses `Environment.MachineName`.

## 2. Request secrets by logical name

Inject `ISecretProvider` and request the secret:

```csharp
string connectionString = await secrets.GetRequiredAsync("cafeteria/database");
```

Recommended naming convention:

```text
{application}/{service}
{application}/{service}/{credential}
```

Examples:

```text
cafeteria/database
cafeteria/slack/webhook
studio/database
studio/graph/client-secret
requestportal/database
```

## 3. Provider rules

Production must explicitly configure the permitted production provider order. Do not include development providers as an automatic production fallback.

Development and test environments may use different values for the same logical name. Application code must not change when the provider changes.

## 4. Telemetry

Common.Secrets emits structured `SecretAuditEvent` and `SecretDiagnosticEvent` records through `ISecretTelemetrySink`. Applications may register a sink before calling `AddCommonSecrets`; the default sink discards telemetry.

Audit records include workload, environment, machine, provider, logical secret name, operation, result, duration and correlation ID. They never contain the secret value.

Diagnostics use stable `SECRETSxxx` codes and intentionally record exception type rather than exception message to reduce credential leakage risk.

## 5. Security rules

Never place production secret values, OpenBao tokens, database passwords, Graph client secrets, Slack webhooks or SMTP passwords in source control.

Never write secret values into logs, audit events, exception messages or diagnostic metadata.

Configuration may contain provider names, addresses, mount paths, workload names and environment names because these are not credentials.

## 6. Common.Diagnostics integration

Keep Common.Secrets independent from Common.Diagnostics at the package level. At the application composition layer, adapt `ISecretProviderHealth.CheckHealthAsync` into a Common.Diagnostics `IDiagnosticCheck`, and route `ISecretTelemetrySink` events to the application's diagnostic/logging pipeline.

This avoids a circular or mandatory dependency while preserving a single diagnostics surface for applications.
