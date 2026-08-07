# Test Results

## Backend

- Command: `dotnet build --configuration Release`
- Command: `dotnet test --configuration Release`
- Result: passed
- Total tests: 529
- Failed tests: 0

## Frontend

- Command: `npm ci`
- Command: `npm run typecheck`
- Command: `npm test`
- Command: `npm run build`
- Result: passed
- Total test files: 27
- Total tests: 145
- Failed tests: 0

## Database

- Command: `dotnet ef migrations list`
- Command: `dotnet ef database update`
- Result: passed; no pending migrations were applied
