# syntax=docker/dockerfile:1.7

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY NuGet.config ./
COPY backend/backend.csproj backend/
RUN dotnet restore backend/backend.csproj

COPY backend/. ./backend/
RUN dotnet publish backend/backend.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_EnableDiagnostics=0
EXPOSE 8080

RUN addgroup --system --gid 10001 app && adduser --system --uid 10001 --ingroup app app
USER app

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "backend.dll"]
