# ADR-0004: Use WhatsApp Cloud API for Guest Messaging

## Status

Accepted

## Context

StayFlow AI targets Airbnb hosts and property managers in Kenya, where WhatsApp is a primary communication channel for guests and operators. The product needs reliable message delivery, webhook handling, and support for conversational workflows.

## Decision

Use WhatsApp Cloud API as the primary messaging integration for guest concierge communication.

## Consequences

- Guest communication can happen in a familiar channel with high adoption.
- The backend must support webhook validation, message processing, retries, and delivery-state tracking.
- Template message rules, opt-in requirements, and platform policies must be respected.
- Messaging integration should be isolated behind services so provider behavior does not leak into business logic.

## Alternatives Considered

- SMS: broad reach, but limited conversational richness and potentially higher message costs.
- Email: useful for formal communication, but weaker for real-time guest concierge interactions.
- Custom mobile app chat: more control, but too much adoption friction for early users.
