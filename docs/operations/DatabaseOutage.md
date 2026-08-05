# Database Outage

## Symptoms

- `/health/ready` fails
- API requests return dependency errors
- EF operations time out

## Immediate Containment

- verify whether the outage is regional or application-specific
- keep read-only/public surfaces available if possible
- preserve logs and alert context

## Recovery

- restore the database or fail over to the latest backup
- recheck readiness and core tenant-scoped flows
