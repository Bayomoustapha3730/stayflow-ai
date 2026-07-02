# 04 AI Orchestrator

## Purpose

The AI orchestrator is the application layer responsible for turning guest messages, property context, company policies, and tool outputs into safe concierge responses.

## Responsibilities

- Load the relevant company, property, guest, and conversation context.
- Retrieve approved property knowledge articles and operational policies.
- Construct prompts with only the information needed for the task.
- Call the AI provider through an integration boundary.
- Validate, log, and persist generated responses.
- Route actions that require tools, service providers, payments, or escalation.

## Boundaries

The orchestrator should not live inside controllers or webhook handlers. It should be implemented as an application service that can be called from webhooks, background workers, or future internal tools.

## Safety Guidelines

- Do not send secrets, tokens, or unnecessary private data to AI providers.
- Prefer structured tool inputs and outputs.
- Keep prompt templates versioned and documented.
- Record enough metadata to evaluate response quality and troubleshoot failures.
- Escalate uncertain or high-risk requests to a human workflow.

## Future Work

Future architecture may include retrieval-augmented generation, tool execution, conversation memory policies, AI evaluation, and response approval workflows.
