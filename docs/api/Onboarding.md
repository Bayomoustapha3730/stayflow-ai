# Onboarding API

Base route: /api/onboarding

## Endpoints

- GET /status
- POST /start
- POST /organization
- POST /plan
- POST /property
- POST /invitations
- POST /whatsapp
- POST /ai-provider
- POST /knowledge
- POST /demo-data
- POST /steps/{step}/skip
- POST /complete
- POST /reset

## Response Shape

Endpoints return ApiResponse and follow existing ProblemDetails/error conventions.

On status-bearing responses, onboarding payload includes:

- currentStep
- currentStepState
- completedSteps
- remainingSteps
- skippedSteps
- blockers
- checklist
- percentComplete
- nextRecommendedAction
- safeLinks
- startedAtUtc
- completedAtUtc/completedByUserId
- version

## Authorization

- status/start: read-only organization members
- mutating step APIs: organization administrator/owner policies
- reset: platform-admin policy

## Notes

- No request accepts tenant ID.
- Plan confirmation uses trusted subscription state.
- Demo data is blocked in production.
- Step skip is allowed only for configured optional steps.
