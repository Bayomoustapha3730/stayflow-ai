# Maintenance

## Business Purpose

Maintenance services help hosts resolve property issues that affect guest comfort, safety, and property condition. This category connects guest-reported problems to trusted technicians and operational tracking.

## User Stories

- As a guest, I want property issues resolved quickly during my stay.
- As a host, I want maintenance requests prioritized by urgency and impact.
- As a technician, I want clear issue details, property access, photos, timing, and approval rules.

## Functional Requirements

- Capture issue category, description, urgency, affected area, property, reservation, guest impact, access notes, preferred time, photos, and approval status.
- Support plumbing, electrical, appliance, internet, HVAC, lock, furniture, and general repair categories.
- Track status from reported to triaged, assigned, scheduled, in progress, resolved, cancelled, or disputed.
- Support emergency maintenance escalation.
- Link maintenance requests to guest, property, reservation, provider, conversation, and audit logs.

## Non-Functional Requirements

- Safety-related issues must be prioritized.
- Access details must be protected and shared only with assigned providers.
- Maintenance status must be visible to host operations.
- Resolution records should support future property quality analytics.

## Validation Rules

- Property, issue category, and description are required.
- Emergency issues must include urgency and escalation path.
- Assigned provider must be approved for the maintenance category.
- Completion should include resolution notes and timestamp.
- Guest-facing updates must avoid exposing provider private information unnecessarily.

## Edge Cases

- Issue is an emergency and guest is in danger.
- Provider needs host approval for additional cost.
- Technician cannot access the property.
- Issue requires multiple visits.
- Guest reports the same issue repeatedly after resolution.

## Acceptance Criteria

- Maintenance documentation supports issue intake, triage, provider assignment, resolution, and escalation.
- Safety, access control, and operational visibility are clearly covered.
- Edge cases account for emergencies, approvals, access failures, and repeat issues.

## Future Enhancements

- Photo-based issue classification.
- Preventive maintenance schedules.
- Vendor quote approval workflow.
- Property reliability analytics.
