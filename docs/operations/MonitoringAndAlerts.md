# Monitoring And Alerts

## Telemetry Goals

Track the following signals:

- request count and latency
- 4xx and 5xx rate
- dependency failures
- readiness failures
- database latency
- AI fallback rate
- WhatsApp send failures
- SignalR reconnects
- concierge action failures
- Host Copilot SLA breaches

## KQL Examples

### Errors By Endpoint

```kusto
requests
| where success == false
| summarize count() by operation_Name, resultCode
| order by count_ desc
```

### Correlation Lookup

```kusto
traces
| where customDimensions.CorrelationId == "<correlation-id>"
| order by timestamp desc
```

### Dependency Failures

```kusto
dependencies
| where success == false
| summarize count() by target, name
| order by count_ desc
```

## Alerts To Configure

- backend readiness failures
- elevated 5xx rate
- p95 latency threshold
- database connection failures
- AI provider failures
- WhatsApp send failures
- authentication failure spikes
- migration failures
- smoke-test failures
