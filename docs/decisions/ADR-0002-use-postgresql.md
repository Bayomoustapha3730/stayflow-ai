# ADR-0002: Use PostgreSQL as the Primary Database

## Status

Accepted

## Context

StayFlow AI stores company, user, property, guest, conversation, knowledge base, service request, payment, and audit data. The database must support relational integrity, indexing, migrations, and future reporting needs.

## Decision

Use PostgreSQL as the primary relational database.

## Consequences

- PostgreSQL provides strong relational integrity, indexing, transactions, and production maturity.
- Entity Framework Core with Npgsql can manage migrations and data access.
- Data modeling must account for company isolation and future multi-tenant scale.
- Operational practices must include backups, migration planning, and monitoring.

## Alternatives Considered

- MySQL: mature relational database, but PostgreSQL offers stronger advanced querying and extension options.
- SQL Server: strong .NET integration, but PostgreSQL is more portable and cost-effective for the expected deployment model.
- MongoDB: flexible document model, but core StayFlow data is relational and benefits from constraints.
