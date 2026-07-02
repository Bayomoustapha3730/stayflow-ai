# Login

## Purpose

The login experience should help authorized StayFlow AI users access their workspace quickly while making authentication errors clear and secure.

## Primary Users

- Company owners.
- Property managers.
- Operations team members.
- Support users.

## UX Goals

- Keep the form focused and uncluttered.
- Support email and password sign-in.
- Provide clear validation messages.
- Avoid revealing whether an account exists.
- Support password reset and email verification workflows.

## States

- Default form.
- Loading while submitting.
- Invalid input.
- Failed authentication.
- Account locked.
- Password reset requested.
- Email verification required.

## Accessibility

- Inputs must have labels.
- Error messages must be screen-reader accessible.
- Keyboard navigation must work without traps.
- Focus should move predictably after validation errors.

## Future Considerations

Add MFA, session timeout messaging, remembered device flows, and single sign-on only after authentication requirements mature.
