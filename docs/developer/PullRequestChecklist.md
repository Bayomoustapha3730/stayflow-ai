# Pull Request Checklist

Use this checklist before requesting review.

## Scope

- The pull request has a clear purpose.
- The change is limited to one feature, fix, or documentation update.
- Unrelated refactors are avoided or explained.

## Implementation

- Controllers remain thin.
- Services contain business behavior.
- Repositories contain persistence behavior.
- DTOs are used at API boundaries.
- Async methods are used for I/O.
- Tenant or company isolation is preserved.

## Validation and Errors

- Input validation is implemented where needed.
- API responses use `ApiResponse<T>`.
- Error messages are useful without exposing sensitive details.

## Testing

- Relevant unit tests are added or updated.
- Existing tests pass.
- Manual testing steps are documented when applicable.

## Documentation

- README files, API notes, ADRs, or operational docs are updated when behavior changes.
- Migrations are documented when database structure changes.
