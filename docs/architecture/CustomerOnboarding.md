# Customer Onboarding Architecture

## Scope

Sprint 20C extends the existing onboarding foundation with resumable, backend-validated tenant onboarding from welcome to first-run readiness.

## Reused Foundations

- Existing onboarding aggregate and API route family in backend onboarding service/controller
- Existing organization profile update flow
- Existing billing plan snapshot and subscription source-of-truth
- Existing property creation and quota enforcement
- Existing organization invitation service
- Existing WhatsApp integration health architecture
- Existing AI provider options and deterministic fallback mode
- Existing property knowledge service

## Workflow Model

Workflow steps:

1. Welcome
2. OrganizationProfile
3. PlanConfirmation
4. FirstProperty
5. TeamInvitations
6. WhatsAppSetup
7. AiProviderSetup
8. KnowledgeBaseSetup
9. DemoData
10. Review
11. Completed

Optional steps:

- TeamInvitations
- WhatsAppSetup
- DemoData

State model:

- Current step
- Completed steps
- Skipped steps
- StartedAtUtc / LastUpdatedAtUtc / CompletedAtUtc
- CompletedByUserId
- IsCompleted
- Version

Persisted server-side in OnboardingProgress.

## Validation Rules

- Step completion is accepted only if prerequisites are satisfied.
- Required steps cannot be skipped.
- Step completion performs backend verification against persisted tenant resources.
- Plan confirmation reads trusted subscription state and does not accept direct billing identity writes from clients.
- Completion checks use computed checklist status from persisted tenant records.

## Security

- Tenant context is always derived from authenticated claims/context.
- No tenant ID is accepted from request body.
- Admin-level onboarding actions require organization administrator/owner policies at API layer.
- Reset endpoint is platform-admin protected.
- Demo data generation is blocked in production environments.
- WhatsApp/AI secrets are never returned to frontend clients.

## Idempotency

- Property step uses deterministic matching to avoid duplicate creation on retries.
- Invitation step de-duplicates within payload and reuses existing invitation service protections.
- Knowledge step checks existing matching records before create.
- Demo data step creates deterministic marker-scoped records and avoids duplicates.

## Observability

- Audit logs are written for all onboarding mutations.
- Privacy-safe onboarding analytics events are written to OnboardingEvents.
- Metadata intentionally excludes secrets, message text, invitation tokens, and provider credentials.
