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
