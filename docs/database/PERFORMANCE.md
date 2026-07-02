# Performance

## Purpose

Database performance guidance helps StayFlow AI remain responsive as companies, properties, guests, conversations, and messages grow.

## Core Practices

- Use pagination for list endpoints.
- Filter company-scoped data by `CompanyId`.
- Use `AsNoTracking()` for read-only EF Core queries.
- Load only the relationships required by the workflow.
- Avoid unbounded queries in API and background jobs.

## Monitoring

Track:

- Slow queries.
- Connection pool pressure.
- Database CPU and memory usage.
- Lock contention.
- Migration duration.
- Table growth for conversations, payments, audit logs, and message-related data.

## Optimization Approach

1. Measure the slow path.
2. Inspect the query and query plan.
3. Confirm indexes match the query pattern.
4. Reduce loaded columns or relationships where possible.
5. Consider caching only after query and index improvements.

## Scaling Considerations

As load grows, consider read replicas, partitioning high-volume tables, background processing, and archiving strategies for historical records.
