# Indexes

## Purpose

Indexes support efficient database access for common StayFlow AI query patterns. They should be intentional, reviewed, and aligned with API and background workflow usage.

## Standard Index Targets

- `CompanyId` for tenant-scoped queries.
- `PropertyId` for property-specific lookups.
- `GuestId` for guest and conversation workflows.
- `PhoneNumber` for guest, contact, and messaging lookups.
- `CreatedAt` for chronological queries, reporting, and audit review.

## Query Pattern Guidance

- List endpoints should filter by tenant scope and use pagination.
- Search endpoints should use indexes where practical and avoid unbounded scans.
- High-volume message and conversation tables should be reviewed as usage grows.
- Compound indexes may be introduced when repeated query patterns justify them.

## Review Guidelines

- Add indexes with migrations when a new query path needs them.
- Avoid adding indexes speculatively without expected query value.
- Review write-heavy tables carefully because indexes add write overhead.
- Use database query plans when investigating performance issues.

## Naming

EF Core generated index names are acceptable by default. Custom names may be used when they improve operational clarity.
