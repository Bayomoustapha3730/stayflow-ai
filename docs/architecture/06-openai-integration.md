# 06 OpenAI Integration

## Purpose

OpenAI powers AI-assisted concierge behavior for guest messaging, knowledge retrieval, recommendation support, and operational assistance.

## Integration Boundary

OpenAI calls should be wrapped behind application services or provider adapters. Business workflows should not depend directly on provider SDK details.

## Prompt Context

Prompts should include:

- Guest request and conversation context.
- Relevant property knowledge articles.
- Company-specific policies.
- Safe operational instructions.
- Output format requirements when structured responses are needed.

Prompts should exclude secrets, credentials, full internal logs, and unnecessary personal data.

## Reliability

- Use timeouts and cancellation tokens.
- Handle provider failures gracefully.
- Log safe metadata such as model, latency, correlation ID, and workflow name.
- Consider retry policies for transient errors.

## Governance

Prompt templates, evaluation notes, and reusable patterns should be documented in `docs/prompts`. Security-sensitive data handling should be documented in `docs/security`.
