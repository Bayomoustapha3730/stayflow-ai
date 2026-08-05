# High Error Rate

## Symptoms

- 5xx rate rises above baseline
- dependency failures increase
- users report broken flows

## Immediate Containment

- compare current revision with the last healthy revision
- look up correlation IDs in logs
- check for a common dependency failure

## Recovery

- rollback if the issue is deployment-related
- otherwise fix the upstream dependency or configuration
