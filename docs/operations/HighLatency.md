# High Latency

## Symptoms

- p95 and p99 request latency rise
- readiness still passes but user flows slow down

## Immediate Containment

- identify whether the spike is backend, database, or dependency related
- inspect slow queries and dependency spans

## Recovery

- scale out the affected Container App if appropriate
- optimize the hot path or dependency timeout if needed
