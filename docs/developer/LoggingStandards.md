# Logging Standards

## Purpose

Logging should help diagnose production issues without exposing sensitive information.

## Guidelines

- Use structured logging with named properties.
- Include correlation IDs in request and error flows.
- Log business-significant events at appropriate levels.
- Avoid logging passwords, tokens, secrets, payment details, or private guest messages.
- Prefer concise logs that identify what happened and where.

## Log Levels

- `Trace`: very detailed diagnostic information, normally disabled.
- `Debug`: development diagnostics.
- `Information`: normal application events.
- `Warning`: unexpected but recoverable situations.
- `Error`: failed operations requiring investigation.
- `Critical`: severe failures that may make the service unavailable.

## Recommended Context

Include relevant identifiers when safe:

- `CorrelationId`
- `CompanyId`
- `PropertyId`
- `UserId`
- Entity type and entity ID

Do not include sensitive payloads unless they are explicitly scrubbed.
