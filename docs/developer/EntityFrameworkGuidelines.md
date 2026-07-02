# Entity Framework Guidelines

## Purpose

Entity Framework Core is the persistence layer for StayFlow AI. These guidelines keep data access predictable, safe, and maintainable.

## Model Configuration

- Use Fluent API configuration classes for entity relationships, indexes, lengths, and constraints.
- Keep EF Core entities in the `Models` folder.
- Keep database access in repositories or persistence-specific services.
- Use GUID primary keys consistently.
- Include audit fields through shared base types where appropriate.

## Queries

- Use `AsNoTracking()` for read-only list queries.
- Apply `CompanyId` filters for company-scoped data.
- Use pagination for list endpoints.
- Avoid loading large object graphs unless the endpoint requires them.

## Migrations

- Review generated migrations before committing.
- Replace drop-and-create operations with table or column renames when preserving data is required.
- Include migrations with model changes.
- Do not edit old migration history unless repairing a local, uncommitted mistake.

## Soft Deletes

- Prefer `IsActive` or another explicit soft-delete flag for business records that should remain auditable.
- Ensure list and detail queries exclude inactive records unless the use case explicitly requires them.

## Transactions

Use explicit transactions when a workflow updates multiple aggregates and partial completion would leave invalid state.
