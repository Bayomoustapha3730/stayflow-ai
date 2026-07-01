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

## Notes

Business modules have intentionally not been added yet. This project only contains the backend foundation.
