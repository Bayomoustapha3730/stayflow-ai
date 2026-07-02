# 01 System Overview

## Purpose

This document describes the high-level architecture of StayFlow AI and the major system boundaries that support an AI-powered WhatsApp concierge platform for Airbnb hosts and property managers.

## Core Capabilities

- Company and user management.
- Property profiles, amenities, rules, recommendations, emergency contacts, and knowledge articles.
- Guest conversations through WhatsApp.
- AI-assisted concierge responses using curated property and company knowledge.
- Service requests, payments, audit logging, and operational workflows.

## Primary Components

- **Backend API**: ASP.NET Core Web API exposing company, property, authentication, and operational endpoints.
- **PostgreSQL Database**: System of record for tenants, users, properties, guests, conversations, and audit data.
- **WhatsApp Integration**: Messaging channel for guest-facing concierge interactions.
- **AI Integration**: OpenAI-powered reasoning and response generation, mediated through application services.
- **Background Workers**: Asynchronous processing for webhooks, notifications, retries, and scheduled jobs.
- **Documentation and Operations**: Engineering, deployment, security, and product guidance under `/docs`.

## Architectural Principles

- Keep company data isolated by default.
- Keep controllers thin and business logic in services.
- Keep persistence behavior in repositories and EF Core configurations.
- Prefer explicit boundaries for external integrations.
- Preserve observability through structured logging, health checks, and correlation IDs.

## Current State

The backend foundation is implemented with ASP.NET Core, Entity Framework Core, PostgreSQL support, Swagger/OpenAPI, global error handling, standardized API responses, authentication groundwork, and property-domain APIs. Future modules should extend the same architectural patterns.
