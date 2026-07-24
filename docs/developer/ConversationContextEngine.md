# Conversation Context Engine (Sprint 9 Part 1)

## Overview

The Conversation Context Engine provides a reusable, tenant-scoped context model for Host Copilot operations.

Implemented backend components:

- `IConversationContextBuilder`
- `ConversationContextBuilder`
- `ConversationContext`
- `ConversationContextSource`
- `ConversationContextWarning`
- `ConversationContextLimits`
- `IContextConfidenceEvaluator`
- `ContextConfidenceEvaluator`
- `ContextConfidenceResult`

The engine is consumed by Copilot summary, suggested replies, and generated draft endpoints.

## Included Context Sections

The context builder returns a normalized immutable model containing:

- Conversation metadata: status, channel, subject, host attention flags, assignment display name.
- Guest display metadata: display name and email (host-visible value only).
- Reservation metadata: reservation id, confirmation number, check-in and check-out dates.
- Property metadata: property id and property name.
- Visible message history: chronological, internal notes excluded, normalized whitespace, stable IDs.
- Approved property knowledge: currently sourced from active `PropertyKnowledgeArticle` records.
- Grounding metadata: sources, warnings, truncation marker, generation timestamp.

## Internal Note Exclusion

Context message history always excludes internal notes (`IsInternal=true`) for guest-facing Copilot output.

## Approved Knowledge Requirement

Approved knowledge for guest-facing context uses active property knowledge records (`PropertyKnowledgeArticle.IsActive=true`).
Inactive records are excluded.

## Limits and Truncation

Config section: `ConversationContext` in backend appsettings.

Default limits:

- Max visible messages: 40
- Max per-message characters: 2000
- Max total prompt-context characters: 16000
- Max knowledge items: 20
- Max per-knowledge-item characters: 4000

Behavior:

- Preserves the earliest guest request where practical.
- Prefers most recent messages.
- Marks `ContextTruncated` warning when limits are applied.
- Normalizes whitespace.
- Avoids logging full message or knowledge content.

## Confidence Heuristic

Confidence starts at 100 and deducts deterministic penalties:

- Missing property: -30
- Missing reservation: -20
- No approved knowledge: -20
- No visible guest message: -25
- Truncated context: -5
- Ambiguous latest guest request: -10
- Conflicting approved knowledge: -20

Range is clamped to 0-100.

Levels:

- High: 80-100
- Medium: 50-79
- Low: 0-49

## Source Metadata

Each included section emits source metadata (`Conversation`, `Reservation`, `Property`, `PropertyKnowledge`) with safe host-facing fields:

- sourceType
- title
- category
- relevanceReason
- lastUpdated

Raw internal identifiers are not exposed to the frontend.

## Reuse Path for Future Providers

The context engine is provider-agnostic and can be reused by:

- future vector retrieval layers
- future external model providers
- prompt preview and orchestration workflows

No external AI provider is called in this sprint as part of context-engine behavior.

## Current Limitations

- Knowledge approval currently maps to `PropertyKnowledgeArticle.IsActive`; there is no separate moderation workflow state.
- Knowledge category normalization is heuristic (title/content keyword-based) for existing records.
- Copilot generated-reply metadata is available via API; UI currently emphasizes summary and suggestion grounding.
