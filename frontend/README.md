# StayFlow Guest Chat Widget

React + TypeScript + Vite guest chat widget for StayFlow AI.

## Status

Sprint 3 uses the authenticated staff/demo flow only. Public guest token support is planned for a later sprint.

## Setup

```bash
npm install
npm run dev
```

Create a local `.env` from `.env.example`. Do not commit credentials or tokens.

The backend must allow the Vite origin in Development. The repository configures `http://localhost:5173` and `http://127.0.0.1:5173` in `backend/appsettings.Development.json`.

## Environment

- `VITE_STAYFLOW_API_URL` - backend API URL, default `http://localhost:5243`
- `VITE_DEMO_EMAIL` - optional demo login email
- `VITE_DEMO_GUEST_ID` - optional demo guest ID
- `VITE_DEMO_RESERVATION_ID` - optional demo reservation ID
- `VITE_DEMO_PROPERTY_ID` - optional demo property ID

Do not store demo passwords in `.env.example`.

## Scripts

- `npm run dev`
- `npm run build`
- `npm run test`
- `npm run lint`
- `npm run typecheck`

## Architecture

See [`../docs/frontend/chat-widget-development.md`](../docs/frontend/chat-widget-development.md) and [`../docs/architecture/guest-chat-widget.md`](../docs/architecture/guest-chat-widget.md).

## Host Copilot Panel

The host conversation view includes a sectioned Copilot panel organized as:

- Conversation Summary
- Sources
- Suggested Replies
- Generate Reply
- Generated Reply

Behavior notes:

- Summary and suggested replies are expanded by default.
- Sources are collapsed by default and support "Show all sources" when more than five sources exist.
- Generated Reply is collapsed when empty and auto-expands after generation or generation errors.
- Section state persists through refresh and resets when switching conversations.

Confidence display:

- Renders a level badge (High, Medium, Low) and clamped percentage when available.
- Uses a semantic meter and optional expandable reasons.

Warnings:

- Hidden when absent.
- Rendered as grouped, host-readable messages when present.

Accessibility and responsive behavior:

- Disclosure sections use native details/summary for keyboard support.
- Copy feedback uses aria-live updates.
- Source chips use list semantics and wrap on narrow screens.
- On desktop, Copilot has an independent scroll area.
- On tablet/mobile, panel sections expand to full width and avoid nested scroll traps.
