# Code Review Checklist

Use this checklist to review StayFlow AI pull requests consistently.

## Correctness

- The implementation satisfies the stated requirement.
- Edge cases and failure paths are handled.
- Tenant and company isolation cannot be bypassed.
- Database relationships, indexes, and constraints match the data model.

## Maintainability

- The code follows existing architecture and naming conventions.
- Responsibilities are placed in the correct layer.
- Abstractions reduce real complexity.
- Comments explain non-obvious decisions without restating the code.

## Security

- No secrets or sensitive data are committed.
- Authentication and authorization assumptions are explicit.
- Logs do not expose credentials, tokens, or private guest data.
- Input validation and error handling are appropriate.

## Testing

- Tests cover success and failure paths.
- Tests are deterministic and focused.
- New migrations are reviewed for destructive operations.

## Documentation

- Public behavior and operational changes are documented.
- ADRs are added for significant architectural decisions.
