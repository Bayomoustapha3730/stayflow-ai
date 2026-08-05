# WhatsApp Outage

## Symptoms

- send failures increase
- webhook acknowledgements fail
- template sync or delivery status checks degrade

## Immediate Containment

- confirm signature checks still pass
- preserve outbox/idempotency state
- avoid duplicate sends during retry

## Recovery

- restore credentials or provider connectivity
- replay only safe, idempotent messages
- verify webhook processing and send telemetry
