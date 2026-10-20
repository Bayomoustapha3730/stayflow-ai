# Sprint 20 Resource Capacity Report

## Scope

Post-fix validation of the Sprint 20 resource-capacity implementation. This report is a diagnostic artifact and remains intentionally untracked.

## Repository state

- HEAD: `8532046f2a25ef0d8c0b41705750b01803abcf4e`
- Upstream: `8532046f2a25ef0d8c0b41705750b01803abcf4e`
- Staged files: none
- `git diff --check`: passed
- Backend migrations: unchanged; no migration created or applied
- Working tree: expected Sprint 20 implementation/test changes remain dirty

## Implementation classification

- **Capacity core:** current-resource capacity service and DI registration
- **Membership integration:** active `OrganizationMember` admission, serializable activation, rollback, and legacy bootstrap
- **Property integration:** non-deleted `Property` admission, serializable creation, and rollback
- **Billing snapshot:** active membership and non-deleted property counts; period metrics unchanged
- **Fail-closed hardening:** required entitlement dependencies for chat, copilot, and property services
- **Tests:** capacity, membership, property, billing, and fail-closed coverage

## Validated behavior

Users capacity counts active organization memberships for the current company. Inactive or removed memberships do not consume seats; removal restores capacity; finite limits reject new admissions; unlimited plans remain unlimited; and historical usage records do not affect admission.

Properties capacity counts non-deleted properties for the current company. Deleted properties do not consume capacity; deletion restores capacity; finite limits reject new creation; unlimited plans remain unlimited; and property admission creates no usage records or operations.

Membership and property admission use serializable transactional paths with rollback on failure. Billing snapshots count current resources while AI, API, storage, file-upload, and WhatsApp period metrics retain their existing semantics.

The legacy membership bootstrap path now bypasses capacity admission for an already-active membership. Missing or inactive memberships still require capacity, preserving idempotency at the seat limit.

Production chat, copilot, and property services require entitlement dependencies. No implicit production NoOp fallback remains. `AllowingSubscriptionEntitlementService` exists only in test code.

## Validation results

- Backend: `1061/1061` passed, `0` failed, `0` skipped
- Focused active-membership test: `1/1` passed
- Frontend: `262/262` passed across `43` files
- Typecheck: passed
- Build: passed
- EF model check: no pending model changes
- Diff whitespace check: passed
- Suspicious-content scan: no actionable secret, credential, debug bypass, disabled-test, or generated-artifact finding

The backend run emitted only the existing ASP.NET Data Protection persistence warning. The focused test emitted the existing XML documentation warning.

## Pending pre-deployment PostgreSQL validation

1. WhatsApp quota races: distinct operation IDs competing for the final unit, same operation ID concurrently, first `UsageRecord` creation race, and serialization/deadlock retry behavior.
2. Two membership activations competing for the final seat.
3. Two property creations competing for the final property slot.

## Safety

- `DefaultConnection` used: no
- Persistent PostgreSQL contacted: no
- Persistent database modified: no
- Migration created/applied: no
- Secrets or PII printed: no
- Force push: no

## Verdict

The local Sprint 20 implementation and test workstream is validated and ready for the separate pre-commit audit. PostgreSQL final-slot concurrency validation remains a pre-deployment requirement.
