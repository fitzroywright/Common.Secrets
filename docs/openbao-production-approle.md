# OpenBao production AppRole baseline

This is the production commissioning baseline for applications using `Common.Secrets` with OpenBao. It deliberately keeps policy and bootstrap mechanics outside application code.

## Rules

- Give every application its own AppRole. Do not share a RoleId or SecretId between applications.
- Give each AppRole read-only access to only its own KV v2 subtree under `secret/data/aegis/<application>/...`.
- Do not grant application roles `create`, `update`, `delete`, `patch`, `sudo`, policy-management, auth-management, or broad `secret/*` capabilities.
- `RoleId` is configuration/identity, not the secret half of the credential. `SecretId` is Secret Zero and must not be committed to source control or normal appsettings.
- In production prefer `SecretIdFile` on a protected, one-time bootstrap file and set `RequireSecretIdFileInProduction=true`. `Common.Secrets` deletes the file after reading by default.
- Keep `RequireHttps=true`, `RenewTokens=true`, and `RevokeTokenOnDispose=true`.
- Issue wrapped or short-lived SecretIds operationally where practical. Rotate a compromised AppRole immediately.

## Per-application policy template

Replace `<application>` with a stable lowercase application path such as `cafeteria`, `studio`, `sensornetwork`, `diagnostics`, or `requestportal`.

```hcl
path "secret/data/aegis/<application>/*" {
  capabilities = ["read"]
}

path "secret/metadata/aegis/<application>/*" {
  capabilities = ["read", "list"]
}
```

If the application never enumerates secrets, omit `list` and the metadata stanza entirely. Prefer the smallest policy that works.

## Role creation baseline

Example operator commands; execute these from an authenticated OpenBao administration session, never from the application:

```sh
bao policy write aegis-<application> aegis-<application>.hcl

bao write auth/approle/role/aegis-<application> \
  token_policies="aegis-<application>" \
  token_ttl="15m" \
  token_max_ttl="1h" \
  token_num_uses=0 \
  secret_id_ttl="30m" \
  secret_id_num_uses=1

bao read auth/approle/role/aegis-<application>/role-id
bao write -f auth/approle/role/aegis-<application>/secret-id
```

Treat the returned SecretId as a one-time bootstrap credential. Deliver it through a protected deployment channel, preferably into a root/administrator-only file consumed by `OpenBaoOptions.SecretIdFile`.

## Application configuration

```json
{
  "CommonSecrets": {
    "OpenBao": {
      "Enabled": true,
      "Address": "https://openbao.example.internal:8200",
      "MountPath": "secret",
      "BasePath": "aegis/<application>",
      "AuthMountPath": "approle",
      "RoleId": "<role-id>",
      "SecretIdFile": "/run/secrets/openbao-secret-id",
      "RequireSecretIdFileInProduction": true,
      "RequireHttps": true,
      "RenewTokens": true,
      "RevokeTokenOnDispose": true
    }
  }
}
```

The RoleId may be delivered through normal protected deployment configuration. The SecretId must not be placed in this JSON.

## Commissioning proof

Before declaring an application commissioned:

1. Confirm login succeeds using its AppRole.
2. Confirm it can read one secret from its own subtree.
3. Confirm it cannot read a different application's subtree.
4. Confirm it cannot write or delete secrets.
5. Confirm the bootstrap SecretId cannot be reused when `secret_id_num_uses=1`.
6. Confirm the bootstrap file is removed after successful consumption.
7. Revoke/expire the client token and confirm `Common.Secrets` reauthenticates without application restart.
8. Stop OpenBao, observe fail-safe behavior, restore OpenBao, and confirm recovery without restart.

Those checks are the acceptance criteria; having an AppRole configured is not by itself proof of production readiness.
