# Groceries

## Business Purpose

Grocery services allow guests to request essentials, breakfast items, water, snacks, baby supplies, or special items before or during a stay. For hosts, grocery coordination can become an upsell and hospitality differentiator.

## User Stories

- As a guest, I want groceries delivered to the property before or during my stay.
- As a host, I want grocery requests tracked so my team does not manage them informally.
- As a provider, I want a clear shopping list, budget, delivery time, and substitution rules.

## Functional Requirements

- Capture shopping list, delivery property, preferred delivery time, budget, substitution preferences, payment status, and notes.
- Support pre-arrival stocking, mid-stay delivery, special dietary requests, and urgent essentials.
- Track request status from requested to quoted, approved, shopping, delivered, cancelled, or disputed.
- Link grocery requests to guest, property, reservation, provider, payment, and conversation.

## Non-Functional Requirements

- Food preference and dietary notes must be handled carefully.
- Pricing and substitutions must be transparent.
- Delivery timing must respect check-in and guest availability.
- Receipts should be retained for dispute resolution.

## Validation Rules

- Delivery property and shopping list are required.
- Budget must be positive when provided.
- Substitution preference should be captured before purchase.
- Payment or host approval should be confirmed before shopping begins.
- Delivery completion should include timestamp and actor.

## Edge Cases

- Requested item is unavailable.
- Guest changes shopping list after purchase begins.
- Provider exceeds budget.
- Delivery arrives before guest check-in.
- Perishable items require refrigeration before guest arrival.

## Acceptance Criteria

- Grocery documentation covers shopping, approval, substitutions, delivery, and payment impact.
- Guest preference and budget handling are clearly defined.
- Edge cases cover unavailable items, timing, and perishable goods.

## Future Enhancements

- Favorite grocery packs per property.
- Supermarket integration.
- Receipt upload and reconciliation.
- Automated pre-arrival grocery offers.
