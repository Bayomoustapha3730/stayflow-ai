# Entity Relationship Diagram

## Purpose

This document describes the high-level entity relationships for the StayFlow AI PostgreSQL data model. It should be updated as major business entities and relationships evolve.

## Core Entity Groups

- **Company Management**: `Company`, `User`, `Role`, `Permission`, `UserRole`, and `RolePermission`.
- **Property Management**: `Property`, `PropertyAmenity`, `PropertyHouseRule`, `PropertyEmergencyContact`, `PropertyRecommendation`, and `PropertyKnowledgeArticle`.
- **Guest Operations**: `Guest`, `Conversation`, `ServiceRequest`, `ServiceProvider`, and `Payment`.
- **Authentication**: `RefreshToken`, `PasswordResetToken`, and `EmailVerificationToken`.
- **Auditability**: `AuditLog` and shared audit fields on auditable entities.

## Relationship Principles

- A `Company` owns users, properties, guests, knowledge, conversations, service providers, service requests, and payments.
- A `Property` belongs to one company and owns its property-specific content.
- A `Guest` belongs to one company and can be associated with conversations and service requests.
- Conversations and service requests should remain company-scoped and property-aware where applicable.
- Authentication support tables should link to users and preserve security workflow history.

## Diagram Guidance

Keep ERDs focused on relationships and cardinality. Avoid overloading diagrams with every column unless the diagram is intended for detailed database review.

## Future Work

Add a generated or manually maintained Mermaid ERD once the core data model stabilizes further.
