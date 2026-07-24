# StayFlow AI Developer Handbook

This handbook defines the engineering practices used to build and maintain StayFlow AI. It is intended for backend, frontend, infrastructure, QA, and AI workflow contributors.

## Contents

- [Coding Standards](CodingStandards.md)
- [Git Workflow](GitWorkflow.md)
- [Branch Strategy](BranchStrategy.md)
- [Pull Request Checklist](PullRequestChecklist.md)
- [Code Review Checklist](CodeReviewChecklist.md)
- [Naming Conventions](NamingConventions.md)
- [Dependency Injection](DependencyInjection.md)
- [Entity Framework Guidelines](EntityFrameworkGuidelines.md)
- [Logging Standards](LoggingStandards.md)
- [Error Handling](ErrorHandling.md)
- [Conversation Context Engine](ConversationContextEngine.md)

## Engineering Principles

- Prefer clear, maintainable implementation over cleverness.
- Keep tenant and company isolation explicit in all data access paths.
- Use asynchronous APIs for I/O-bound work.
- Keep controllers thin and place business behavior in services.
- Keep repository methods focused on persistence concerns.
- Document decisions that affect architecture, security, data models, or delivery workflow.

Update this handbook when team conventions change so new contributors can work confidently without relying on tribal knowledge.
