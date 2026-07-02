# Guests API

## Purpose

Guest APIs will manage guest profiles, contact details, property stay context, conversation linkage, and guest support history.

## Planned Endpoint Areas

- Guest creation and update.
- Guest lookup by company, property, and phone number.
- Guest conversation history.
- Guest service request history.
- Soft delete or deactivation workflows where appropriate.

## Data Considerations

Guest records are company-scoped and may be associated with properties, conversations, payments, and service requests. Phone number lookups should be indexed and tenant-scoped.

## Privacy Notes

Guest data may include personal information. API responses should return only the fields required by the workflow, and logs should avoid exposing private guest content.

## Future Documentation

Add concrete routes, DTO schemas, validation rules, and examples when the guest module is implemented.
