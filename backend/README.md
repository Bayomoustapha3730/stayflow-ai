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

Authentication is intentionally not implemented yet. Company management is the first business module.
