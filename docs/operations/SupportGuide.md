# Support Guide

## Purpose

This guide describes how support engineers use Sprint 20D SaaS operations tools safely.

## Access Requirements

- User must have `platform.admin` permission.
- Support impersonation requires an explicit authorization code and reason.

## Support Console Workflow

1. Navigate to `/platform-admin` and open `support`.
2. Enter target tenant ID and target user ID.
3. Enter explicit authorization code.
4. Provide clear support reason.
5. Start impersonation session.
6. Resolve support task.
7. End impersonation session immediately.

## Audit Expectations

The platform logs:

- `SupportImpersonationStarted`
- `SupportImpersonationEnded`

Do not perform support work without paired start/end audit events.

## Tenant Lifecycle Operations

Use tenant actions only with documented incident/customer context:

- Suspend
- Reactivate
- Archive
- Restore
- Repair

Each operation creates an immutable platform audit record.

## Safety Boundaries

- Never expose API keys, provider secrets, or webhook secrets.
- Do not alter tenant identity context through support workflows.
- Keep operations reversible where possible (reactivate/restore).
