# 13 Cache Strategy

## Purpose

Caching can improve performance and reduce repeated database or provider calls, but it must not weaken tenant isolation or serve stale operationally sensitive data.

## Cache Candidates

- Read-heavy reference data.
- Permission metadata.
- Property knowledge summaries.
- AI prompt support data that changes infrequently.
- Provider configuration that is safe to cache.

## Guidelines

- Do not cache secrets or sensitive tokens in application memory without a clear security design.
- Include company scope in cache keys for tenant-specific data.
- Use short expirations for data that changes frequently.
- Invalidate cache entries on updates when correctness matters.
- Prefer measuring performance issues before introducing caching.

## Current State

The current backend does not require a dedicated cache layer. PostgreSQL indexes and efficient queries should be used first.

## Future Options

Future cache infrastructure may include in-memory caching for single-instance development and distributed caching for scaled production deployments.
