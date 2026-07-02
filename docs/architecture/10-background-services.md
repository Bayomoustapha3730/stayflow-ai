# 10 Background Services

## Purpose

Background services handle work that should not block HTTP requests, such as message processing, retries, scheduled jobs, notification delivery, and long-running AI workflows.

## Candidate Workloads

- WhatsApp webhook processing.
- AI response generation.
- Message delivery retries.
- Email verification and password reset delivery.
- Scheduled reminders and follow-ups.
- Cleanup of expired tokens and stale conversations.
- Operational reports and analytics preparation.

## Design Guidelines

- Persist work before processing when reliability matters.
- Use idempotent handlers to tolerate retries.
- Include correlation IDs and safe business identifiers in logs.
- Keep retry policies bounded.
- Separate background concerns from controller code.

## Failure Handling

Failed jobs should be visible to operators, retried when safe, and moved to a dead-letter or manual review workflow when repeated failures occur.

## Future Work

The implementation may use hosted services, a queue-backed worker, or managed background job infrastructure depending on scale and deployment needs.
