# Operations and testing gates

Common.Secrets is Green only when its security and resilience behavior is deliberate and repeatable.

## Required scenarios

The automated and integration test suites should cover:

- bootstrap succeeds with an approved workload identity;
- bootstrap fails closed when identity is missing or invalid;
- provider outage is surfaced without leaking credentials;
- provider recovery restores normal retrieval;
- fully offline development uses only the approved local provider;
- expired credentials are rejected or renewed according to policy;
- rotated credentials replace old credentials without logging either value;
- production provider selection never falls through to a development provider;
- missing optional secrets produce a controlled NotFound result;
- missing required secrets fail explicitly;
- audit events identify workload, machine, environment, provider and logical secret name;
- diagnostic and audit serialization contains no secret values.

## CI security gate

Tests must inject recognizable canary values representing common secret classes, including a database password, connection string, OpenBao token, Graph client secret and webhook. Serialize all captured audit/diagnostic events and assert that none of the canary values occur.

A secret-leakage test failure blocks release.

## Stable diagnostic codes

| Code | Meaning |
| --- | --- |
| SECRETS001 | Provider health check succeeded |
| SECRETS021 | Secret retrieval succeeded |
| SECRETS022 | Secret not found |
| SECRETS031 | Provider operation failed |
| SECRETS032 | Provider health check failed |

Codes are stable machine-readable identifiers. Human-readable messages may evolve, but must never include secret values.

## Audit fields

Every observed secret read records:

- UTC timestamp;
- workload;
- environment;
- machine/instance;
- provider;
- logical secret name;
- operation;
- result;
- duration;
- correlation ID.

The audit contract intentionally has no secret-value field.
