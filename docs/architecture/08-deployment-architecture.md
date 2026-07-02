# 08 Deployment Architecture

## Purpose

Deployment architecture describes how StayFlow AI services move from source code to reliable runtime environments.

## Environments

- **Local**: developer machines using local configuration and development database settings.
- **Staging**: production-like environment for release validation.
- **Production**: customer-facing environment with managed secrets, monitoring, backups, and controlled releases.

## Components

- ASP.NET Core backend service.
- PostgreSQL database.
- Background worker runtime when asynchronous processing is introduced.
- External integrations for WhatsApp, OpenAI, email, and payments.
- Monitoring and logging infrastructure.

## Release Guidance

- Run automated tests before deployment.
- Review EF Core migrations before applying them.
- Apply migrations through a controlled release process.
- Keep rollback procedures documented.
- Validate health checks after deployment.

## Configuration

Environment-specific configuration should come from secure configuration providers or environment variables, not committed source files.
