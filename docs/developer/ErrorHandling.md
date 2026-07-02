# Error Handling

## Purpose

StayFlow AI should return predictable API errors while keeping internal implementation details private.

## API Responses

- Use standardized `ApiResponse<T>` objects.
- Include clear messages for validation and not-found cases.
- Include validation errors as structured error entries.
- Preserve correlation IDs so failures can be traced in logs.

## Exceptions

- Use global exception handling middleware for unexpected errors.
- Do not expose stack traces, connection strings, tokens, or internal exception details in API responses.
- Throw exceptions for exceptional states, not normal validation flow.
- Prefer explicit result responses for expected business failures.

## Validation

- Validate request DTOs before changing state.
- Return `400 Bad Request` for malformed or invalid input.
- Return `404 Not Found` when a resource does not exist or is outside the caller's company scope.
- Return `403 Forbidden` for authorization failures once authorization is enabled.

## Logging

Unexpected exceptions should be logged with correlation ID and safe context. Sensitive data must be redacted.
