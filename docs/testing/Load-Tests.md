# Load Tests

## Purpose

Load tests evaluate StayFlow AI behavior under sustained or peak traffic so the team can understand capacity limits before users experience them.

## Candidate Scenarios

- Concurrent company and property API usage.
- WhatsApp inbound message bursts.
- AI response generation during peak guest activity.
- Service request creation and status updates.
- Analytics dashboard usage.
- Authentication and refresh token traffic.

## Guidelines

- Run load tests against non-production environments unless explicitly approved.
- Use synthetic or anonymized data.
- Avoid overwhelming paid external providers during tests.
- Apply rate limits and test budgets.
- Monitor application, database, and external dependency metrics during the test.

## Outputs

Each load test should document:

- Scenario and assumptions.
- Test duration.
- Traffic shape.
- Environment configuration.
- Bottlenecks observed.
- Recommended follow-up actions.

## Future Work

Define target capacity goals after initial customer usage patterns are available.
