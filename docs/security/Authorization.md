# Authorization

## Purpose

Authorization determines what authenticated users can do within StayFlow AI.

## Core Model

- Users belong to a company.
- Roles group permissions.
- Permissions grant access to specific capabilities.
- Company scope must be enforced for tenant-owned data.

## Guidelines

- Enforce authorization at API boundaries and service workflows.
- Do not rely on frontend checks for security.
- Treat cross-company access as a security failure.
- Return `403 Forbidden` for insufficient permissions when authentication is present.
- Return `404 Not Found` when revealing resource existence would leak another company's data.

## Permission Design

Permissions should be explicit, stable, and aligned with business actions, such as managing properties, viewing guests, handling service requests, or managing billing.

## Future Work

Add a complete permission matrix for roles such as owner, admin, property manager, support operator, and read-only user.
