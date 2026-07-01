# StayFlow AI Backend

ASP.NET Core Web API foundation for StayFlow AI, an AI-powered WhatsApp concierge platform for Airbnb hosts and property managers in Kenya.

## Stack

- .NET 10 (`net10.0`)
- ASP.NET Core Web API
- Entity Framework Core
- PostgreSQL via Npgsql
- OpenAPI enabled in development
- Health checks at `/health`
- Global exception handling middleware

## Project Structure

```text
backend/
  Controllers/
  Data/
  DTOs/
  Extensions/
  Middleware/
  Models/
  Repositories/
  Services/
```

## Setup

1. Install the .NET SDK.
2. Start PostgreSQL locally.
3. Update `ConnectionStrings:DefaultConnection` in `appsettings.Development.json`.
4. Restore and run the API:

```bash
dotnet restore
dotnet run
```

## Useful Endpoints

- `GET /health` - health check endpoint
- `GET /openapi/v1.json` - OpenAPI document in development
- `GET /api/status` - lightweight backend status endpoint
- `GET /companies` - paginated company list with optional `search`
- `GET /companies/{id}` - company details
- `POST /companies` - create company
- `PUT /companies/{id}` - update company
- `DELETE /companies/{id}` - soft delete company
- `POST /auth/login` - JWT login
- `POST /auth/refresh` - refresh token rotation
- `POST /auth/password-reset` - generate password reset token
- `POST /auth/password-reset/confirm` - reset password
- `POST /auth/email-verification/confirm` - verify email
- `GET /auth/me` - current authenticated user
- `GET /roles` - role list
- `POST /roles` - create role
- `POST /roles/{roleId}/permissions` - assign permission to role

## Database

Restore the local EF Core tool before working with migrations:

```bash
dotnet tool restore
```

Apply migrations to the configured PostgreSQL database:

```bash
dotnet tool run dotnet-ef database update --project backend/backend.csproj --startup-project backend/backend.csproj
```

Run tests:

```bash
dotnet test tests/StayFlow.Api.Tests/StayFlow.Api.Tests.csproj
```

## Notes

Authentication uses JWT bearer tokens, refresh token rotation, PBKDF2 password hashing, account lockout, and role/permission tables. Outbound email delivery is not implemented yet, so reset and verification endpoints currently return generated tokens for development workflow only.
