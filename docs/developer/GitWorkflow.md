# Git Workflow

## Purpose

The Git workflow ensures every change can be reviewed, tested, and traced back to a clear reason.

## Standard Workflow

1. Start from the latest `main`.
2. Create a focused branch for one feature, fix, or documentation change.
3. Make small, coherent commits with clear messages.
4. Run relevant tests and checks locally.
5. Open a pull request using the repository template.
6. Address review feedback with additional commits.
7. Merge only after required checks and approvals pass.

## Commit Guidance

- Use concise imperative messages, such as `Add property validation tests`.
- Keep unrelated changes in separate commits or pull requests.
- Do not commit generated artifacts unless they are required by the project, such as EF Core migrations.
- Never commit secrets, local configuration, database dumps, or private customer data.

## Before Pushing

- Run the relevant test suite.
- Review `git diff` for accidental changes.
- Confirm migrations are intentional when database models change.
- Confirm documentation is updated when behavior or workflow changes.
