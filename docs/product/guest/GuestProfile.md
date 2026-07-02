# Guest Profile

## Business Purpose

The guest profile stores the operational facts needed to identify and support a guest. It should help hosts serve guests quickly while avoiding unnecessary collection of sensitive personal data.

## User Stories

- As a host, I want to see a guest's name, phone number, and active stay context.
- As a support user, I want notes and tags that help me understand guest needs.
- As a guest, I want my profile information to be accurate and used only for service delivery.

## Functional Requirements

- Store name, phone number, email, preferred contact method, country, language, tags, notes, and status.
- Support company-level uniqueness checks for phone and email where appropriate.
- Allow profile updates without deleting conversation or stay history.
- Track created, updated, and deleted audit metadata.

## Non-Functional Requirements

- Profile data must be secure and company isolated.
- Profile retrieval should be fast for common support screens and WhatsApp routing.
- The design must support partial profiles from early-stage contacts.

## Validation Rules

- Name should be required once a confirmed booking or manual guest record exists.
- Phone number must be normalized when provided.
- Email must use a valid email format when provided.
- Notes should have length limits and should not store payment card details or highly sensitive data.

## Edge Cases

- Guest has no email address.
- Guest is known only by phone number at first contact.
- Guest changes phone number mid-stay.
- Multiple guests are part of the same booking.
- Host enters duplicate guest records manually.

## Acceptance Criteria

- Guest profile fields and boundaries are clearly documented.
- Validation expectations protect data quality without blocking partial guest intake.
- Profile data supports both manual operations and automated concierge lookup.

## Future Enhancements

- Guest profile completion scoring.
- Duplicate guest suggestions.
- Import mapping from Airbnb and direct booking systems.
- Custom profile fields per company.
