# Companies API

## Purpose

Company APIs manage the tenant boundary for StayFlow AI. A company represents an Airbnb host organization or property management business.

## Current Endpoint Areas

- `GET /companies`
- `GET /companies/{id}`
- `POST /companies`
- `PUT /companies/{id}`
- `DELETE /companies/{id}`

## Supported Behavior

- Pagination for list responses.
- Search by company name.
- Soft delete behavior.
- Audit logging for create, update, and delete operations.
- Standardized `ApiResponse<T>` response format.

## Data Considerations

Companies are the root scope for users, properties, guests, conversations, service requests, payments, and knowledge content. API workflows that create company-owned records should validate the company exists and is active.

## Future Documentation

Add detailed request and response examples, validation rules, and admin authorization requirements once authorization is enforced.
