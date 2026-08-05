# AI Provider Outage

## Symptoms

- AI generation falls back frequently
- provider requests time out or fail
- relevant fallback counters increase

## Immediate Containment

- confirm deterministic fallback remains active
- do not retry in a tight loop
- watch for elevated latency or 5xx rates

## Recovery

- fix the provider configuration or key
- verify fallback clears once the provider recovers
