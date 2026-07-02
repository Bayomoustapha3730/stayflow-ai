# Acceptance Criteria

## Purpose

This document defines product-level acceptance criteria for the property domain.

## Property Profile

- A user can create a property for an active company.
- A user can view a company-scoped list of active properties.
- A user can search properties by name.
- A user can view property details with nested property content.
- A user can update property profile fields.
- A user can soft delete or deactivate a property.

## Nested Property Content

- A property can include amenities.
- A property can include house rules.
- A property can include emergency contacts.
- A property can include local recommendations.
- A property can include knowledge base articles.
- Updating nested content should not expose inactive or deleted content in active responses.

## Validation

- Required property fields must be validated.
- Required nested content fields must be validated.
- Invalid requests should return standardized errors.
- Company ID must be required until authenticated tenant context is fully enforced.

## Company Isolation

- Property list, detail, update, and delete workflows must be company-scoped.
- A user should not access another company's property data.
- Cross-company access should not expose sensitive resource existence.

## Auditability

- Property create, update, and delete operations should be audit logged.
- Audit logs should include safe entity and company context.

## API Behavior

- Property APIs should return standardized `ApiResponse<T>` objects.
- List endpoints should support pagination.
- Not-found and validation cases should produce predictable response messages.

## AI Readiness

- Active property knowledge should be available for future AI concierge workflows.
- Inactive property content should not be used in guest-facing AI responses.
