# Known Issues

- Frontend test runs still emit existing React `act(...)` warnings in `BillingDashboardPage.test.tsx` and `PlanComparisonPage.test.tsx`.
- Vite build output still shows the known Rollup circular-dependency warnings from SignalR-related dependencies.
- GitHub CLI review automation is unavailable in this container because `gh` is not installed.
- The workspace currently contains generated test output under `tests/StayFlow.Api.Tests/TestResults/`.
