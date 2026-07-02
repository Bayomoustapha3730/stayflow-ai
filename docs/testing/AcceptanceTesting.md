# Acceptance Testing

## Purpose

Acceptance testing verifies that StayFlow AI meets product expectations from the perspective of hosts, property managers, guests, marketplace providers, and administrators.

## Scope

- Company onboarding and management.
- Property setup and knowledge base readiness.
- Guest profile and conversation workflows.
- Reservation lifecycle from booking through checkout.
- AI concierge response and escalation.
- Marketplace service request fulfillment.
- Billing plan, invoice, payment, commission, refund, and tax workflows.

## Scenario Format

Use a clear Given, When, Then format:

```text
Given a host has an active company and property
When a guest asks for check-in instructions through WhatsApp
Then StayFlow AI responds using the correct property knowledge
And the response does not expose unauthorized access details
```

## Guidelines

- Tie acceptance tests to documented product requirements.
- Include happy paths and critical failure paths.
- Validate standardized API responses where backend behavior is involved.
- Include mobile-first guest communication assumptions for WhatsApp workflows.
- Confirm manual escalation paths are usable when automation cannot proceed.

## Release Readiness

- Critical workflows have passed automated or manual acceptance checks.
- Known gaps are documented with owner and mitigation.
- Product owner accepts behavior against documented criteria.
- Regression risks are reviewed before deployment.

## Acceptance Criteria

- Acceptance tests reflect real customer workflows.
- Each major product domain has at least one acceptance scenario.
- Release decisions can reference documented acceptance evidence.
