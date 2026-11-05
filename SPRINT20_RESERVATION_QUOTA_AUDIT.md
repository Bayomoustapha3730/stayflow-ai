# SPRINT 20 — RESERVATION QUOTA SEMANTICS & ENFORCEMENT

## BASELINE

Verified read-only baseline from repository state:

- Branch: main
- HEAD: 7903c9f047a9cd16c82bd92b39d9117f7941ed47
- Upstream: 7903c9f047a9cd16c82bd92b39d9117f7941ed47
- Working tree source state: clean except for an untracked, pre-existing diagnostic artifact: SPRINT20_RESOURCE_CAPACITY_REPORT.md
- No source or test files were modified. No stage or commit happened. No database migration was created or applied.

The repo is at the approved baseline commit described in the audit brief.

## 1. RESERVATION QUOTA DEFINITION: CURRENT SOURCE EVIDENCE

### Enumeration and canonical metric definition

- File: backend/Models/UsageMetric.cs
- Definition: `public enum UsageMetric { Users, Properties, Reservations, AiRequests, AiTokens, WhatsAppMessages, ApiRequests, StorageBytes, FileUploads }`
- Extension: `UsageMetricExtensions.ToQuotaEntitlementKey()` generates `Quota.{metric}`.
- This establishes the canonical quota key for the reservation metric as `Quota.Reservations`.

### Seeded plan limits

- File: backend/Data/SeedData.cs
- `BuildPlanEntitlements()` assigns:
  - Free: `Quota.Reservations = 200`
  - Starter: `Quota.Reservations = 2000`
  - Professional: `Quota.Reservations = 15000`
  - Enterprise: `Quota.Reservations = null` (unlimited)

### Entitlement calculation and current usage snapshot authority

- File: backend/Services/SubscriptionEntitlementService.cs
- `BuildSnapshotAsync(...)` loads all `PlanEntitlement` rows whose Key starts with `Quota.` and matches a known `UsageMetric`.
- For `UsageMetric.Users` and `UsageMetric.Properties`, the snapshot calculates `used` from the current live counts.
- For all other metrics, including `Reservations`, `used` is read from `UsageRecords` for the current billing period.
- This shows the current architecture treats `Reservations` as a period-based quota metric in the same family as AI requests, WhatsApp messages, etc., not as a current-resource count.

### Billing snapshot exposure

- File: backend/Controllers/OrganizationPlanController.cs
- `GetUsage()` returns `QuotaUsageDto` items from `SubscriptionSnapshot.Quotas`.
- `MapQuota()` exposes `Metric`, `Limit`, `Used`, `Remaining`, `IsUnlimited`, and billing period start/end.
- This is a generic billing viewer: it is not reservation-specific.

### Usage endpoint exposure

- File: backend/Controllers/PlatformAdminController.cs
- `GetUsage()` aggregates `UsageRecords` over the last 30 days and includes `ReservationsLast30Days = usage.Where(item => item.Metric == UsageMetric.Reservations.ToStorageValue()).Sum(item => item.QuantityUsed)`.
- This proves platform admin surface consumes the `UsageRecords` table for `Reservations`, not the `Reservations` table itself.

### Enforcement calls

- File: backend/Services/SubscriptionEntitlementService.cs
- Public method: `ConsumeQuotaAsync(Guid companyId, UsageMetric metric, long quantity, string idempotencyKey, CancellationToken cancellationToken)`
- This is the enforcement mechanism for all quota metrics, including `Reservations`, if a caller invokes it.

### UsageRecord writes

- File: backend/Models/UsageRecord.cs
- Model fields: `CompanyId`, `Metric`, `PeriodStartUtc`, `PeriodEndUtc`, `QuantityUsed`.

### UsageOperation writes

- File: backend/Models/UsageOperation.cs
- Model fields: `CompanyId`, `Metric`, `IdempotencyKey`, `PeriodStartUtc`, `Quantity`.

### `ConsumeQuotaAsync` calls

Search across production code found no call sites for `ConsumeQuotaAsync` with `UsageMetric.Reservations`.

This is the most important finding: the metric exists in the plan model and generic billing stack, but there is no production `Reservations` quota admission path today.

### Resource-capacity logic

- File: backend/Services/ResourceCapacityService.cs
- `EnsureCapacityAsync(...)` supports only `Users` and `Properties` in `GetCurrentCountAsync`.
- It explicitly throws for other metrics: `Resource capacity is only defined for Users and Properties.`
- This is a direct architectural exclusion of “current active reservation capacity” from the resource-capacity framework.

### Tests

- File: tests/StayFlow.Api.Tests/SubscriptionEntitlementServiceQuotaTests.cs
- Tests cover the generic quota mechanism for WhatsAppMessages, not `Reservations`.
- No reservation-specific quota tests exist.

## 2. CURRENT PLAN LIMITS

Canonical plan limits sourced from the current seed data:

| Plan | Reservation limit | Unlimited? | Feature dependency |
| --- | ---: | --- | --- |
| Free | 200 | No | None in source; this is an entitlement entry only |
| Starter | 2,000 | No | None in source; this is an entitlement entry only |
| Professional | 15,000 | No | None in source; this is an entitlement entry only |
| Enterprise | null | Yes | None in source; `IsUnlimited = true` |

Current semantic description in source:

- `PlanEntitlement.Key` is `Quota.Reservations`.
- `Unit` is set to `"count"` in the migration.
- There is no plan-specific annotation saying monthly, current active, or concurrent.
- The generic billing implementation uses a `UsageRecord`/`UsageOperation` period model, which makes the effective semantics a period-consumption metric by default unless a caller writes a different usage source.

## 3. RESERVATION DOMAIN MODEL: ACTUAL STRUCTURE

### Model

- File: backend/Models/Reservation.cs
- Fields present:
  - `Id`
  - `CompanyId`
  - `PropertyId`
  - `PrimaryGuestId`
  - `ExternalReservationReference`
  - `ReservationSource`
  - `ConfirmationNumber`
  - `CheckInDate`
  - `CheckOutDate`
  - `Adults`
  - `Children`
  - `TotalGuestCount`
  - `Status`
  - `Currency`
  - `BookingAmount`
  - `SpecialRequests`
  - `InternalNotes`
  - `IsActive`
  - `IsDeleted`
  - `DeletedAt`
  - `DeletedBy`

- Related configuration: backend/Data/Configurations/ReservationConfiguration.cs
- Query filter: `builder.HasQueryFilter(reservation => !reservation.IsDeleted);`

### Status model

- File: backend/Models/ReservationStatus.cs
- States: `Draft`, `PendingConfirmation`, `Confirmed`, `PreArrival`, `ReadyForCheckIn`, `CheckedIn`, `ActiveStay`, `CheckOutPending`, `CheckedOut`, `PostStay`, `Completed`, `Cancelled`, `NoShow`

### Lifecycle and domain behavior

- File: backend/Services/ReservationStatusTransitionPolicy.cs
- Valid transitions are strictly defined and operationally oriented.
- Reservation lifecycle semantics are active and operational, not quota semantics.

### Can a reservation be…

- Created manually? Yes, via `ReservationService.CreateAsync(...)` and controller `POST /reservations`
- Created by API? Yes, same endpoint is an authenticated tenant API route
- Imported? There is no dedicated import service in the current source; imported ids are represented as `ExternalReservationReference`/`ReservationSource`, but the app does not expose a separate import engine.
- Synced from PMS/channel manager? Not as a dedicated production subsystem in current source. `ReservationSource` can carry external labels like `Airbnb` or `Demo`, but no PMS sync implementation was found.
- Created from guest conversation? Indirectly, a conversation can bind to a reservation, but the core reservation is still created by domain/service logic and not a guest conversation side-effect.
- Automatically created? Yes, demo and onboarding paths create sample reservations, but they are provisioning/demo artifacts, not quota-enforced commercial reservations.
- Updated? Yes, `ReservationService.UpdateAsync(...)`
- Cancelled? Yes, status transitions include `Cancelled` via `TransitionStatusAsync(...)`
- Soft-deleted? Yes: `IsDeleted`, `DeletedAt`, `DeletedBy` and query filter
- Hard-deleted? Not via normal API; soft delete is the implemented deletion pattern
- Reactivated? Not via explicit API; the model has `IsActive`, but no dedicated restore method
- Duplicated/replayed? There is no reservation-level duplicate prevention beyond the general unique index on `(CompanyId, ReservationSource, ExternalReservationReference)` and the general error/validation path. This is not an idempotent quota identity boundary.

## 4. EVERY RESERVATION CREATION PATH

### Path 1: Tenant-scoped manual reservation API

- Entry point: backend/Controllers/ReservationsController.cs `CreateReservation`
- Service: backend/Services/ReservationService.cs `CreateAsync`
- Repository mutation: `ReservationRepository.AddAsync` -> `dbContext.Reservations.AddAsync(reservation)`
- Tenant authority: tenant context company id via `ICurrentTenantContext`
- Authentication: `[Authorize]` on controller
- Commercial/provisioning/development: commercial tenant API path, not a demo/provisioning bypass
- Idempotency: none at reservation-create level; only object validity and tenancy checks
- Transaction: method saves via `reservationRepository.SaveChangesAsync()` without explicit transaction
- Can it create duplicates? Yes, in principle, if two requests supply the same or different external references without dedupe logic; the code does not enforce a reservation idempotency policy
- Current quota enforcement: none
- Current usage metering: none

### Path 2: Onboarding demo/sample reservation creation

- File: backend/Services/OnboardingService.cs
- If `request.CreateSampleReservation` is set, it creates a reservation with `ReservationSource = "Demo"` and `ExternalReservationReference = "{marker}:reservation"`.
- This is provisioning/demo setup, not a commercial materialization.
- Current quota enforcement: none
- Current usage metering: none

### Path 3: Demo seed data

- File: backend/Services/DevelopmentSeedService.cs
- Ensures demo reservations with source `Airbnb` / `DemoSeed`.
- Not a customer billing path and not a commercial quota boundary.
- Current quota enforcement: none
- Current usage metering: none

### Path 4: Background lifecycle/event work

- Files: backend/Services/ReservationLifecycleEventGenerator.cs and related lifecycle services
- These operate on existing reservation rows, generate lifecycle events, and are not reservation-creation paths.
- They do not admit quota or metric writes.

### Path 5: Conversation or AI context linking

- Conversation and AI context resolution bind to existing reservations; they do not create reservations as a business rule.

### Conclusion

There is no single canonical commercial reservation creation boundary with quota admission. The code currently has multiple manual, demo, and onboarding creation pathways, but no production quota admission on reservation creation.

## 5. UPDATE / CANCEL / DELETE PATHS AND QUOTA CONSEQUENCES

### Update

- File: backend/Services/ReservationService.cs `UpdateAsync`
- Changes property, guest, dates, amounts, notes, `IsActive`.
- This can change a reservation’s operational status and date range, but there is no quota logic tied to modification.

### Status transition

- File: backend/Services/ReservationService.cs `TransitionStatusAsync`
- Uses `ReservationStatusTransitionPolicy` to enforce legal state changes.
- Cancelled is a valid transition.
- No quota side-effects exist.

### Soft delete

- File: backend/Services/ReservationService.cs `DeleteAsync`
- Sets `IsDeleted = true`, `DeletedAt = DateTimeOffset.UtcNow`, `DeletedBy = currentTenantContext.UserId`.
- Query filter excludes deleted rows.
- For any period-consumption model, deletion normally should not refund usage because the usage was already admitted when the reservation was created; but if the product is later defined as current active resources, deletion could restore capacity.

### Cancellation

- Cancellation is a status change, not a record delete.
- Under a period-consumption model, cancellation should not refund quota already used. Under a current-active model, cancellation could reduce active count and free capacity.

### Restore/reactivate

- No current restore path exists. There is no explicit reactivation of a soft-deleted or cancelled reservation.

## 6. CURRENT BILLING BEHAVIOR

### Subscription entitlements and usage architecture

- File: backend/Services/SubscriptionEntitlementService.cs
- `ConsumeQuotaAsync` writes a single `UsageRecord` per `(company, metric, periodStartUtc)` and an `UsageOperation` with `IdempotencyKey` for each valid admitted operation.
- This is exactly the architecture used for non-resource quotas such as WhatsApp and AI requests.

### Reservations today

The current production code does not show any `ConsumeQuotaAsync` call for:

- `UsageMetric.Reservations`
- `Quota.Reservations`
- reservation creation admission
- reservation cancellation refund
- reservation deletion refund

Therefore, the current `Reservations` quota in billing is effectively dormant. It is a seeded entitlement key, but the metric is not consumed in production.

### Can the UI display 0 / 2,000 even when real reservations exist?

Yes. It can, because the UI is fed from `UsageRecord`/`SubscriptionSnapshot` data. If no `UsageRecord` row exists for `Reservations`, the displayed use is `0`, even when there are real rows in `Reservations`.

### Could historical `UsageRecords` disagree with actual `Reservation` rows?

Yes, and the architecture permits it. The current code does not write `Reservations` usage from the reservation table, so historical usage can be stale, zero, or absent while actual reservation rows remain in the database.

### Current billing display authority

- `OrganizationPlanController.GetUsage()` and `PlatformAdminController.GetUsage()` both derive values from `UsageRecords` only.
- The `Reservations` table is not a source-of-truth for usage under this codebase.

## 7. BILLING PERIOD SEMANTICS

### Paid subscriptions

- File: backend/Models/TenantSubscription.cs
- `CurrentPeriodStartUtc` and `CurrentPeriodEndUtc` are authoritative for the current billing period.
- File: backend/Services/BillingService.cs merges provider snapshot data into the subscription, including Stripe `current_period_start` and `current_period_end` when present.
- File: backend/Services/SubscriptionEntitlementService.cs `GetOrCreateCurrentSubscriptionAsync` sets current period from the first day of the current month to the last instant of the month.

### Trial

- `TenantSubscription` also has `TrialEndsAtUtc` but the quota system itself is keyed to current subscription period rather than a separate trial usage frame.

### Free

- Free plan is provisioned as the default plan and still uses the same current-period-based quota architecture.

### Plan change

- `UpdatePlanAsync` changes `SubscriptionPlanId` and leaves the current period start/end in place unless the billing provider event updates them.
- The current quota model is therefore period-aware and subscription-period anchored.
- If `Quota.Reservations` were changed to a period-consumption metric, usage would naturally roll with the active period; it would not be auto-inherited across a new period unless a new `UsageRecord` starts at zero.
- If the period changes, historical usage for prior periods becomes invisible to the current snapshot and the snapshot intentionally uses only records matching the current `CurrentPeriodStartUtc` and `CurrentPeriodEndUtc`.

## 8. SEMANTIC OPTIONS ANALYSIS

### Option A — Reservations created per billing period

Definition: each unique logical reservation admitted during the billing period consumes one reservation unit. Updates, cancellation, and deletion do not refund. Retry/replay of the same logical reservation does not consume more than one unit.

Advantages:
- Fits the existing `UsageRecord` + `UsageOperation` + `CurrentPeriodStartUtc`/`CurrentPeriodEndUtc` architecture.
- Matches the current billing framework that is already period-scoped.
- Matches the plan entitlements seeded as numeric `count` quotas.
- Naturally aligns with billing, sales, and customer communication.

Disadvantages:
- Requires durable idempotency and a reservation logical identity beyond raw row ID.
- Must decide how to treat imported reservations, PMS sync, and duplicate external provider events.
- Must define admission semantics carefully if row insertion fails after quota admission.

Required identity:
- Need a stable logical operation identity such as a provider/source/external reservation key, or a durable reservation-creation dedupe key.

Idempotency:
- Must dedupe same logical reservation across retries or duplicate webhooks.

Imports/PMS sync:
- Must decide whether imported reservation rows count as new admissions when first observed during the period.

Plan changes and downgrade:
- Current-period usage should remain tied to the active subscription period; downgrade should apply only from the current period onward, unless retroactive enforcement is introduced.

Cancellation/deletion:
- Normally no refund after usage has been admitted.

### Option B — Current active reservations

Definition: reservation quota is a capacity limit on currently active/non-cancelled reservations.

Advantages:
- Intuitive for capacity-limited operations.
- Matches some resource-capacity patterns in the current code for Users and Properties.

Disadvantages:
- Directly contradicts the current `UsageRecord`/`UsageOperation` architecture, which is period-based and not live-count based.
- `ResourceCapacityService` explicitly supports only Users and Properties as live counters.
- There is no code path for current active reservation count logic or quota enforcement.
- Cancellation and deletion have immediate effect on capacity, which is a different semantics than the rest of quota stack.

### Option C — Reservation-nights / stays

Definition: quota is based on nights, completed stays, or occupied nights.

Applicable?
- Not strongly supported by the source.
- The current product language and plan entitlements are `count`-based, not `nights` or `stays`-based.
- No reservation-night metric exists in enum, billing, or seed data.
- The current `UsageMetric` family has no `ReservationNights` or `StayedNights` metric.

Conclusion: Option C is not supported by source/product language and should not be selected without a new metric design.

## 9. CUSTOMER-FACING LANGUAGE

Search of frontend, docs, billing, and pricing text found no explicit customer-facing wording stating:

- "reservations per month"
- "monthly reservations"
- "bookings per month"
- "reservation limit"
- "active reservations” quota in pricing text

The only nearby product language is operational and product-architecture wording such as:

- docs/product/reservation/ActiveStay.md: "multiple active reservations"
- docs/product/reservation/README.md: "one phone number maps to multiple active reservations"
- docs/product/guest/GuestStayHistory.md: "prior and current stays through reservations"

The plan/pricing artifacts instead discuss higher-level commercial dimensions such as:

- property count
- team members
- AI usage
- WhatsApp volume
- features

There is no current customer-facing pricing language that clearly defines `Quota.Reservations` as a monthly, active, or nightly metric.

## 10. LOGICAL RESERVATION IDENTITY

If the product selects period consumption, the safest durable identity is not a raw `Reservation.Id` alone, because that only exists after insertion. For a real quota admission boundary, we need a stable logical operation identity deduplicated across retry or duplicate provider events.

Possible durable identity types:

- `Reservation.Id` after row creation — usable for post-creation dedupe and audit, not for pre-insert admission
- `ExternalReservationReference` + `ReservationSource` + company + source-specific external key
- `ConfirmationNumber` + `ReservationSource` + company
- provider id + external reservation reference
- API idempotency key if the client supports it

Safest canonical billable operation identity:
- A stable source-specific logical key such as:
  - `companyId + reservationSource + externalReservationReference`
  - or provider-specific `provider|externalId`
- If the reservation is created directly by StayFlow, then `Reservation.Id` can be used after creation as a durable audit identity but not as the only pre-admission identity.

For a pre-creation admission boundary, the product would need an idempotency key established before DB write or a durable dedupe table keyed by the external identity. That means a schema addition is likely required if the product insists on admitting before insert and protecting against duplicate original creation attempts.

## 11. FAILURE SEMANTICS

### A. Quota admission succeeds but Reservation DB insert fails

Under a period-consumption model, the correct behavior depends on whether quota admission and reservation insert share a transaction.

- If quota is admitted before insertion and the insert fails, the quota is over-consumed unless rollback is implemented.
- In a same-database transaction, the preferred design is to reserve quota and insert the reservation in one transaction so the quota can roll back with the reservation failure.
- This is stronger than the current WhatsApp quota behavior, which intentionally allows admission to remain consumed after provider failure because the external provider call is not relationally tied to the system-of-record in the same way.

### B. Reservation insert succeeds but UsageOperation fails

This creates an unmetered reservation relative to quota. That is undesirable for a real quota decision.

The correct design is to ensure quota admission and UsageOperation write are in the same transaction boundary or that the authoritative database state is updated atomically.

### C. Retry after ambiguous failure

Need a durable idempotency key to avoid double counting the same logical reservation across retries.

### D. Concurrent final-unit creation

With a limit of 200 and usage of 199, two distinct logical reservations arriving concurrently must not both be admitted if only one final unit remains.

The correct behavior requires:

- a serializable or row-level lock strategy around quota rows
- idempotent operation logging per logical reservation
- `UsageOperation` uniqueness on the logical reservation identity or on a reservation-admission key

The existing generic `ConsumeQuotaAsync` uses a serializable transaction and `UsageOperation` uniqueness for idempotency, which is the right structural foundation. However, it is still a generic quota admission mechanism and does not solve reservation-specific logical identity by itself.

## 12. IMPORT / PMS / EXTERNAL SYNC POLICY

The current architecture does not show a dedicated PMS/channel-manager sync subsystem. It does have external identity fields and source labels, but no strong import boundary enforcement.

That means the correct policy must be decided explicitly before implementation.

Recommended default based on actual architecture:

- Do not count historical imported reservations that predate customer onboarding.
- Do not count repeated resyncs of the same external reservation.
- Count only newly observed logical reservations in the active billing period, if the external source is part of the active product flow.
- Do not count direct system/demo/provisioning records that are not customer-facing commercial reservations.

This is more consistent with `UsageRecord` billing semantics than a current active total over all imported rows.

## 13. ONBOARDING / SEED / DEMO EXCEPTIONS

The following are operational provisioning paths and should not be treated as commercial quota admissions:

- backend/Services/OnboardingService.cs `CreateSampleReservation`
- backend/Services/DevelopmentSeedService.cs demo reservation seed
- demo company seed data and demo property seed data

These create rows in `Reservations`, but they are not customer-commercial fulfillment and should bypass reservation quota by design.

This is different from a commercial billing boundary: such paths should not count toward a customer’s plan entitlement unless the product explicitly wants to count onboarding-created reservations as part of a real commercial lifecycle.

## 14. API ERROR CONTRACT

Current quota exhaustion in the generic system is represented by `QuotaExceededException`:

- File: backend/Exceptions/QuotaExceededException.cs
- Constructor: `QuotaExceededException(string metric, long? limit, long requested, long current)`
- Message: `Quota exceeded for metric '{metric}'.`

The generic HTTP delivery is not reservation-specific, but the current framework is built around throwing a quota-exceeded exception that then surfaces through middleware or API error handling. The intended convention is therefore:

- HTTP status: 400 or 403 depending on how the exception is translated by the service layer
- Error code: follows existing domain validation / quota exception contracts, not reservation-specific
- Frontend handling: generic quota exception handling is expected; there is no reservation-specific UI handling in source

There is no current reservation API that explicitly handles a reservation quota exhaustion scenario.

## 15. CONCURRENCY REQUIREMENTS

For a period-consumption definition, the required invariant is:

- limit = 200
- current usage = 199
- two distinct logical reservations in the same period
- result: at most one new logical reservation may consume the final unit

For same logical reservation retried concurrently:

- only one background execution should win and count once

The existing architecture already provides the core primitives:

- `ConsumeQuotaAsync` uses a serializable transaction
- `UsageOperation` stores an idempotency key
- `UsageRecords` are unique per company/metric/period

This is sufficient for generic quota admission. For reservation quota, the missing piece is the logical identity and the reservation admission boundary linking quota reservation to actual reservation creation.

This should be validated with a PostgreSQL-backed test path or an integration test strategy. The current in-memory test infrastructure is enough for generic quota logic, but not for proving relational transaction semantics under PostgreSQL’s serializable guarantees.

## 16. MIGRATION ASSESSMENT

For a correct reservation quota implementation under period consumption, a migration is likely required if the product requires durable idempotency and dedupe across retries or imports.

Reasons:

- `UsageOperation` already has a generic idempotency pattern, but it is not reservation-specific.
- `Reservation` has no dedicated logical identity field for a billable creation key.
- `ExternalReservationReference` and `ConfirmationNumber` are not a guaranteed durable single source of truth across all creation paths.
- Duplicate provider events and import retries require a stronger unique index or a dedicated reservation quota claim table.

Therefore: MIGRATION LIKELY REQUIRED for the recommended Option A implementation.

## 17. TEST GAP MATRIX

| Test area | Status |
| --- | --- |
| finite limit | MISSING |
| unlimited plan | MISSING |
| tenant isolation | MISSING |
| final unit | MISSING |
| over-limit rejection | MISSING |
| retry same logical reservation | MISSING |
| concurrent same identity | MISSING |
| concurrent distinct identities | MISSING |
| failed DB creation | MISSING |
| cancellation | NOT APPLICABLE until semantic is chosen |
| deletion | NOT APPLICABLE until semantic is chosen |
| update | NOT APPLICABLE until semantic is chosen |
| historical UsageRecord | PARTIAL (generic quota tests exist for WhatsApp, not Reservations) |
| billing snapshot | PARTIAL (generic snapshot infrastructure exists) |
| plan downgrade | PARTIAL (generic usage snapshot logic exists, reservation-specific not tested) |
| import/sync | MISSING |
| provisioning bypass | PARTIAL (demo provisioning exists, but no reservation quota exemption test) |
| quota error contract | MISSING |

## 18. SCOPE BOUNDARY

The eventual reservation quota work should exclude the following unless evidence later shows an exception:

- Users capacity
- Properties capacity
- WhatsApp quota
- AI request quota
- AI token metering
- API request metering
- StorageBytes
- FileUploads
- registration/invitation architecture
- unrelated onboarding
- provider billing checkout

## 19. PRODUCT RECOMMENDATION

Recommendation: Option A — reservations created per billing period.

Why this matches StayFlow architecture:

- The generic quota system is already period-based through `UsageRecord` and `UsageOperation` keyed by `CurrentPeriodStartUtc`/`CurrentPeriodEndUtc`.
- The reservation metric is seeded as a `Quota.Reservations` entitlement with `count` unit and plan limits.
- There is no reservation-specific live resource capacity mechanism in the codebase.
- `ResourceCapacityService` explicitly supports only Users and Properties as live capacity counters.
- Platform admin usage aggregates `UsageRecords` over the last 30 days, not the current reservation table.

Meaning of the metric:

- Counts: each unique logical reservation admitted during the active billing period, once
- Does not count: updates, status transitions, cancellations, soft deletes, and replayed duplicates
- Cancellation: should not refund a previously admitted reservation quota unit
- Deletion: should not refund a previously admitted reservation quota unit
- Updates: consume zero additional units
- Retries: same logical reservation should not count twice
- Imports/sync: should be treated as either a new logical reservation admission when first observed or excluded if they are historical bootstrap rows; product decision is needed
- Plan changes: should be assessed on current-period quotas according to the active subscription timeline
- Downgrade: should be enforced from the current period onward, not by retroactively deleting prior usage
- Billing snapshot authority: `UsageRecord` + `UsageOperation` is the authoritative billing record under the current pattern

## 20. IMPLEMENTATION BOUNDARY (FOR FUTURE APPROVAL)

Likely production files to review/change:

- backend/Models/UsageMetric.cs
- backend/Data/SeedData.cs
- backend/Services/SubscriptionEntitlementService.cs
- backend/Services/ReservationService.cs
- backend/Models/Reservation.cs
- backend/Data/Configurations/ReservationConfiguration.cs
- backend/Controllers/ReservationsController.cs
- backend/Controllers/OrganizationPlanController.cs
- backend/Exceptions/QuotaExceededException.cs

Potential new abstractions:

- `IReservationQuotaAdmissionService`
- a reservation admission idempotency key builder
- a dedupe policy object for provider/external source identity

Likely tests to add/change:

- tests/StayFlow.Api.Tests/SubscriptionEntitlementServiceQuotaTests.cs
- new reservation-specific quota tests

Frontend:

- likely no change unless billing UI is specifically allowed to display reservations quota; current source does not expose a reservation-specific UI element

Migration:

- likely required if the implementation needs a durable logical identity or unique admission record for reservation quotas

PostgreSQL validation:

- recommended before finalizing transaction semantics, especially for serializable and concurrent final-unit behavior

## 21. SAFETY

- Source modified: No
- Tests modified: No
- DB modified: No
- Persistent PostgreSQL contacted: No
- DefaultConnection used: No
- Migration created/applied: No
- Reservation created/updated/deleted: No
- Committed: No
- Pushed: No
- Secrets/PII printed: No

## 22. PHASE 2 IMPLEMENTATION RESULT

The approved period-consumption semantics were implemented without changing the generic quota behavior used by WhatsApp, AI, API, storage, users, or properties.

### Implementation

- `ReservationService.CreateAsync` now routes the commercial reservation creation boundary through `AdmitReservationAsync`.
- Reservation logical identity is `reservation:{Reservation.Id:N}`.
- Reservation persistence and reservation usage mutation are tracked in the same EF unit of work and committed atomically inside the reservation-specific serializable transaction.
- Exhausted quota is evaluated before reservation persistence is invoked.
- Failed persistence clears the tracked quota mutation; relational transactions roll back both reservation and usage state.
- Replay of the same logical reservation identity is idempotent and does not invoke persistence a second time.
- Update, status transition, cancellation, and soft deletion paths do not consume or refund reservation quota.
- Onboarding sample reservations and development seed reservations remain explicit non-commercial exceptions.

### Focused tests

- `ReservationServiceTests`: 37 passed, 0 failed, 0 skipped.
- `SubscriptionEntitlementServiceQuotaTests`: 12 passed, 0 failed, 0 skipped.

### Full validation

- Backend: 1,065 passed, 0 failed, 0 skipped; duration 85.4s.
- Frontend: 43 test files, 262 passed, 0 failed, 0 skipped.
- Frontend typecheck: passed.
- Frontend production build: passed with non-fatal chunk-size warnings.
- EF pending-model check: no pending model changes.
- `git diff --check`: passed.

### Final report status

IMPLEMENTATION COMPLETE — READY FOR PRE-COMMIT AUDIT
