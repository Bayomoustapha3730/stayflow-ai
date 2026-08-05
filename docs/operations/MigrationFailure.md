# Migration Failure

## Symptoms

- migration script exits non-zero
- database schema mismatch
- readiness or API failures after migration

## Immediate Containment

- stop the rollout
- keep the last healthy revision active
- avoid re-running destructive steps until reviewed

## Recovery

- inspect the generated idempotent migration SQL
- restore from backup if required
- reapply only the reviewed script after approval
