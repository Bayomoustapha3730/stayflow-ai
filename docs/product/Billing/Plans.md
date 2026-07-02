# Plans

## Business Purpose

Plans package StayFlow AI capabilities into clear commercial tiers. They define what each company can use, how much it costs, and which limits apply to properties, users, conversations, AI usage, and marketplace features.

## User Stories

- As a host, I want to compare plans before choosing one.
- As a property manager, I want a plan that scales with my portfolio.
- As a product owner, I want plan limits to map cleanly to feature access.

## Functional Requirements

- Define plan name, price, currency, billing interval, feature set, usage limits, add-ons, and availability.
- Support free trial, starter, growth, professional, and enterprise-style packaging.
- Control access to AI features, WhatsApp messaging, analytics, marketplace tools, and support levels.
- Track plan version so historical subscriptions and invoices remain accurate.

## Non-Functional Requirements

- Plan definitions must be understandable to customers and enforceable by the platform.
- Plan changes must not break historical billing records.
- Feature entitlements should be easy to evaluate at runtime.
- Pricing display should support Kenyan Shilling and future multi-currency expansion.

## Validation Rules

- Plan name and billing interval are required.
- Price must be non-negative.
- Currency is required for paid plans.
- Retired plans should not be available for new subscriptions.
- Feature limits should have explicit unlimited or numeric values.

## Edge Cases

- Customer is subscribed to a retired plan.
- Promotional price differs from standard plan price.
- Enterprise plan has custom contract terms.
- Downgrade would exceed plan limits.
- Feature limit changes between plan versions.

## Acceptance Criteria

- Plan documentation defines packaging, limits, entitlements, and versioning.
- Requirements support pricing transparency and future entitlement enforcement.
- Edge cases cover retired plans, promotions, custom contracts, and downgrades.

## Future Enhancements

- Plan comparison page.
- Add-on marketplace.
- Promotional coupon support.
- Feature entitlement service.
