# Properties API

## Purpose

Property APIs manage rental property profiles and guest-facing concierge content.

## Current Endpoint Areas

- `GET /properties?companyId={companyId}`
- `GET /properties/{id}?companyId={companyId}`
- `POST /properties`
- `PUT /properties/{id}`
- `DELETE /properties/{id}?companyId={companyId}`

## Supported Content

- Property profile details.
- Amenities.
- House rules.
- Emergency contacts.
- Local recommendations.
- Property knowledge articles for AI concierge responses.

## Supported Behavior

- Company-scoped list, detail, update, and delete operations.
- Pagination and search.
- Validation before create and update.
- Soft deletes for property records and nested property content.
- Audit logging for state-changing operations.
- Standardized `ApiResponse<T>` response format.

## Tenant Isolation

Property reads and writes must include company scope until authenticated tenant context is fully enforced. Cross-company access should not expose another company's property data.
