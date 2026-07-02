# Acceptance Tests

## Purpose

Acceptance tests confirm that StayFlow AI features meet business and product expectations from a user or workflow perspective.

## Scope

Use acceptance tests for:

- Host onboarding.
- Company setup.
- Property profile creation.
- Guest messaging workflows.
- AI concierge response behavior.
- Service request lifecycle.
- Payment and reconciliation workflows.
- Reporting and analytics workflows.

## Format

Acceptance criteria should describe:

- User or actor.
- Starting condition.
- Action.
- Expected outcome.
- Error or edge case behavior.

Example:

```text
Given a property manager has an active company
When they create a property with amenities and house rules
Then the property is available through company-scoped property APIs
And nested property content is returned in the property detail response
```

## Guidelines

- Keep acceptance tests tied to user value.
- Include failure paths for important workflows.
- Keep test data realistic.
- Record gaps found during acceptance testing as backlog items.
