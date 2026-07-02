# ADR-0003: Use OpenAI for AI Concierge Capabilities

## Status

Accepted

## Context

StayFlow AI needs AI-powered concierge capabilities for guest communication, property knowledge retrieval, recommendations, and operational assistance. The AI layer should support natural language understanding, response generation, and future tool-based workflows.

## Decision

Use OpenAI as the primary AI provider for concierge intelligence.

## Consequences

- The product can deliver high-quality conversational experiences faster.
- Prompt design, evaluation, safety controls, and monitoring become core engineering responsibilities.
- Sensitive data sent to AI systems must be minimized, governed, and documented.
- The architecture should isolate AI provider integration so future provider changes remain possible.

## Alternatives Considered

- Self-hosted open-source models: greater control, but higher operational complexity and quality risk.
- Other hosted AI providers: viable alternatives, but OpenAI provides strong model capability and developer tooling.
- Rule-based chatbot logic: predictable, but too limited for a concierge experience with varied guest requests.
