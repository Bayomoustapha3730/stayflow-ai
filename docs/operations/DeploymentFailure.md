# Deployment Failure

## Symptoms

- deployment workflow fails
- new revision never becomes healthy
- smoke tests fail after rollout

## Immediate Containment

- stop promotion to production
- keep previous revision active
- capture the failing SHA and workflow logs

## Recovery

- fix the deployment artifact or configuration
- redeploy the last known good image SHA
- verify `/health/live`, `/health/ready`, and smoke tests
