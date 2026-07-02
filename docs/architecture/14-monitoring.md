# 14 Monitoring

## Purpose

Monitoring provides visibility into StayFlow AI health, performance, reliability, and business-critical workflows.

## Signals

- API availability and latency.
- Error rates by endpoint and workflow.
- Database query performance.
- WhatsApp webhook and delivery failures.
- AI provider latency, failures, and token usage.
- Background job failures and retry counts.
- Authentication failures and account lockouts.

## Logging

Logs should be structured, include correlation IDs, and avoid sensitive data. Important business identifiers such as `CompanyId`, `PropertyId`, and `ConversationId` may be logged when safe.

## Health Checks

The backend exposes health checks and should expand them to cover database and critical dependencies as production readiness increases.

## Alerts

Alerts should focus on customer impact, data risk, and operational failures. Avoid noisy alerts that do not require action.

## Dashboards

Dashboards should show service health, integration status, message processing, and high-level product usage trends.
