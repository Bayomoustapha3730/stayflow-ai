# Branch Strategy

## Purpose

The branch strategy keeps development organized while protecting the stability of `main`.

## Primary Branches

- `main`: production-ready source of truth. It should always build and pass required tests.
- Feature branches: short-lived branches used for individual features, fixes, documentation updates, or experiments.

## Branch Naming

Use descriptive branch names:

- `feature/property-management`
- `fix/auth-refresh-token`
- `docs/developer-handbook`
- `chore/update-dependencies`

## Rules

- Keep feature branches short-lived.
- Rebase or merge from `main` before final review when needed.
- Avoid combining unrelated work in one branch.
- Delete branches after merge.
- Do not force-push shared branches unless the team has agreed to it.

## Release Readiness

Before merging to `main`, confirm:

- The change is reviewed.
- Tests pass.
- Database migrations are reviewed when present.
- Security and tenant isolation impacts are understood.
- Documentation is updated.
