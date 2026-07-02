# Private Chef

## Business Purpose

Private chef services help hosts offer premium hospitality experiences for guests who want in-property meals, celebrations, family dining, or local cuisine. This category can generate marketplace revenue and elevate guest satisfaction.

## User Stories

- As a guest, I want to request a chef for a meal or event at the property.
- As a host, I want chef requests to respect property rules and kitchen limitations.
- As a chef, I want guest count, menu preferences, timing, kitchen access, and payment expectations.

## Functional Requirements

- Capture meal date, time, guest count, cuisine preference, dietary restrictions, budget, kitchen access, event type, and special instructions.
- Support breakfast, lunch, dinner, celebration meals, meal prep, and local cuisine experiences.
- Track status from requested to quoted, approved, scheduled, in progress, completed, cancelled, or disputed.
- Link chef requests to guest, property, reservation, provider, payment, and conversation.

## Non-Functional Requirements

- Dietary and allergy information must be handled accurately and carefully.
- Property kitchen constraints must be visible before provider confirmation.
- Pricing, ingredients, and service fees must be transparent.
- Service quality and safety must be reviewable.

## Validation Rules

- Meal date, guest count, and property are required.
- Guest count must be greater than zero.
- Dietary restrictions should be confirmed before service.
- Provider must be approved for private chef services.
- Host approval may be required for events or high guest counts.

## Edge Cases

- Guest reports a severe allergy.
- Kitchen equipment is unavailable or broken.
- Guest changes menu close to service time.
- Event violates house rules.
- Chef cancels after ingredients are purchased.

## Acceptance Criteria

- Private chef documentation supports premium guest service workflows.
- Dietary, property, approval, and pricing considerations are explicit.
- Edge cases cover safety, cancellation, and policy conflicts.

## Future Enhancements

- Curated menu packages.
- Chef availability calendar.
- Ingredient cost estimates.
- Guest review and tipping flow.
