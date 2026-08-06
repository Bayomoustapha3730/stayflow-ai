# Multi-Tenant Architecture (Sprint 19A)

## Decision: Company Is the Tenant

StayFlow uses `Company` as the first-class tenant boundary.

Why:

- Existing domain entities, JWT claims, repositories, and services are already company-scoped.
- Introducing a second tenant root (`Tenant`) in parallel would duplicate ownership semantics and increase migration risk.
- `Company` has been evolved to carry organization metadata (`Slug`, `Status`, `OwnerUserId`, optional branding, onboarding state) without breaking existing IDs or relationships.

## Tenant Resolution

Active tenant resolution is handled by authenticated context services:

- `ITenantContext`
- `TenantContext`
- Backward-compatible alias `ICurrentTenantContext`

Resolution order:

1. Trusted execution context (`ITenantExecutionContextAccessor`) for internal/background workflows.
2. Authenticated JWT claims (`company_id`, with `tenant_id` alias support).

Request bodies are not authoritative for tenant selection.

## Authorization Roles

Organization roles:

- `Owner`
- `Administrator`
- `Manager`
- `Host`
- `Support`
- `ReadOnly`

Tenant-aware policies are enforced using active membership checks from `OrganizationMembers`:

- `organization.owner`
- `organization.administrator`
- `organization.manager`
- `organization.host`
- `organization.support`
- `organization.readonly`

## Membership Model

`OrganizationMembers` tracks per-tenant membership:

- `CompanyId` (tenant)
- `UserId`
- `Role`
- `Status`
- `JoinedAt`
- `InvitedByUserId` (optional)

Protection:

- Filtered unique index enforces no duplicate active membership per `(CompanyId, UserId)`.
- Sole-owner removal is blocked.

## Data Isolation Strategy

Primary isolation remains explicit service/repository scoping by `CompanyId`.

Global query filters remain limited to soft-delete/business-state filters to avoid breaking:

- migrations
- seeding
- administrative tasks
- background jobs

Bypass strategy:

- Cross-tenant maintenance must be explicit and audited.
- If `IgnoreQueryFilters` is used in future admin tooling, tenant checks still apply on writes.

## Write Protection Strategy

`ApplicationDbContext` save pipeline validates tenant ownership:

- Rejects missing `CompanyId` for tenant-owned entities.
- Rejects cross-tenant writes when authenticated tenant context is active.
- Rejects `CompanyId` mutation on updates.
- Rejects cross-tenant foreign-key associations to tenant-owned principals.

## SignalR and Webhook Isolation

SignalR:

- Conversation joins require authenticated tenant context.
- Conversation access is validated via company-scoped repository lookups.
- Company host channels are namespaced by company identifier.

WhatsApp webhooks:

- Integration is resolved first, then execution is pinned to integration company context.
- Guest and reservation resolution is company-scoped.
- Duplicate/stale IDs are checked in company scope.

## Migration Approach

Migration: `AddMultiTenantFoundation19A`

Non-destructive changes:

- Extend `Companies` with organization metadata fields.
- Create `OrganizationMembers`.
- Backfill slug/status values for all existing companies.
- Backfill active membership rows from existing users.
- Preserve and infer owner assignment using existing role/user order.
- Keep all existing records and table structures.

## Background Job Strategy

Background and async processors that execute tenant-owned writes should set execution tenant context before persistence operations. Existing webhook processing follows this pattern.

## Known Limitations

- `Role`/`Status` are stored as strings for compatibility; stricter enum persistence can be introduced later.
- Company-level global query filters are intentionally not enabled globally in this phase.
- Invitations lifecycle is not included in Sprint 19A.
- Billing/subscription and API key management are intentionally out of scope.
