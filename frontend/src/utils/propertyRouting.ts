const guidPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;

export function normalizePropertyId(value: string | null | undefined): string | null {
  if (typeof value !== "string") {
    return null;
  }

  const trimmed = value.trim();
  if (trimmed.length === 0) {
    return null;
  }

  return guidPattern.test(trimmed) ? trimmed : null;
}

export function resolvePropertyKnowledgePropertyId(
  selectedConversationPropertyId: string | null | undefined,
  configuredDemoPropertyId: string | null | undefined,
  allowDemoFallback = true
): string | null {
  const selected = normalizePropertyId(selectedConversationPropertyId);
  if (selected) {
    return selected;
  }

  if (!allowDemoFallback) {
    return null;
  }

  return normalizePropertyId(configuredDemoPropertyId);
}
