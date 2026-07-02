# Tables

## Purpose

This document describes the purpose of major database tables in StayFlow AI.

## Company and Identity

- `Companies`: Tenant boundary for property managers and host organizations.
- `Users`: System users who belong to a company.
- `Roles`: Named role definitions.
- `Permissions`: Permission definitions used by authorization workflows.
- `UserRoles`: Join table between users and roles.
- `RolePermissions`: Join table between roles and permissions.

## Property Domain

- `Properties`: Rental units or managed properties.
- `PropertyAmenities`: Guest-facing property amenities.
- `PropertyHouseRules`: Rules and expectations for guests.
- `PropertyEmergencyContacts`: Emergency contacts available for a property.
- `PropertyRecommendations`: Local recommendations associated with a property.
- `PropertyKnowledgeArticles`: Property-specific AI knowledge content.

## Guest and Operations

- `Guests`: Guest records scoped to a company.
- `Conversations`: Guest communication sessions.
- `ServiceProviders`: Vendors or service providers.
- `ServiceRequests`: Operational requests such as maintenance, cleaning, or guest support.
- `Payments`: Payment records and payment-related state.

## Security and Audit

- `RefreshTokens`: Refresh token lifecycle and rotation support.
- `PasswordResetTokens`: Password reset workflow tokens.
- `EmailVerificationTokens`: Email verification workflow tokens.
- `AuditLogs`: Business and administrative audit events.

## Table Design Rules

- Use GUID primary keys.
- Include `CreatedAt` and `UpdatedAt` on auditable records.
- Include `IsActive` for records that support soft deletion.
- Keep company-scoped tables explicitly linked to `CompanyId` where appropriate.
