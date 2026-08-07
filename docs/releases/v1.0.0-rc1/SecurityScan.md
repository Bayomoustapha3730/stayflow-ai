# Security Scan

## Local scan commands

- `dotnet list package --vulnerable --include-transitive`
- `dotnet list package --outdated`
- `npm audit --omit=dev`
- `npm audit`

## Status

Results captured in this workspace:

- Backend vulnerable packages: none found.
- Frontend production dependencies: 0 vulnerabilities.
- Frontend all dependencies: 0 vulnerabilities.
- Backend outdated packages: updates available for `Microsoft.AspNetCore.Authentication.JwtBearer`, `Microsoft.AspNetCore.OpenApi`, `Microsoft.EntityFrameworkCore.Design`, `Microsoft.OpenApi`, and `Npgsql.EntityFrameworkCore.PostgreSQL`.

The available package updates are informational for the candidate and do not block the release by themselves.
