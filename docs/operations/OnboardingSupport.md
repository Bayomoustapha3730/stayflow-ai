# Onboarding Support Runbook

## Common Checks

1. Confirm user has active tenant membership.
2. Check onboarding status via GET /api/onboarding/status.
3. Review blockers and checklist fields from status response.
4. Confirm required dependencies:
   - active subscription snapshot
   - first property existence
   - AI provider readiness mode
   - optional WhatsApp integration health

## Troubleshooting

- Onboarding not started:
  - Call POST /api/onboarding/start.
- Plan confirmation blocked:
  - Verify billing flow completion and active subscription snapshot.
- Property step blocked:
  - Validate quota limits and tenant ownership.
- Invitation step failures:
  - Check duplicate/active invitation constraints and role validity.
- Demo data blocked:
  - Confirm environment is not production.

## Reset/Recovery

- Reset is restricted to platform-admin policy.
- Use POST /api/onboarding/reset with confirm=true.
- Reset clears completed/skipped steps and completion timestamps.

## Data Safety

- Onboarding analytics and audit logs must remain privacy-safe.
- Do not include secrets, provider tokens, invitation raw tokens, or message text in logs.
