# Audit Logging

## Purpose

Audit logging records important security and business events so StayFlow AI can support accountability, investigation, and compliance needs.

## Events to Audit

- Company creation, update, and deletion.
- Property creation, update, and deletion.
- User role or permission changes.
- Authentication failures and account lockouts.
- Password reset and email verification events.
- Payment status changes.
- Security configuration changes.
- Cross-company access attempts where detectable.

## Required Context

Audit events should include safe identifiers such as:

- Correlation ID.
- Company ID.
- User ID.
- Entity name and entity ID.
- Action name.
- Timestamp.
- Safe details about the change.

## Data Safety

Audit logs should not include passwords, token values, payment secrets, or private guest conversation content.

## Future Work

Define retention periods, export procedures, administrator views, and alerting for high-risk audit events.
