import { useEffect, useMemo, useState } from "react";
import {
  PropertyKnowledgeCategory,
  propertyKnowledgeCategoryLabels,
  propertyKnowledgeCategoryOptions
} from "../../models/propertyKnowledge";

export interface PropertyKnowledgeFormSubmission {
  category: PropertyKnowledgeCategory;
  title: string;
  summary: string;
  content: string;
  tags: string[];
  priority: number;
  isActive: boolean;
}

export interface PropertyKnowledgeFormDraft {
  category: PropertyKnowledgeCategory;
  title: string;
  summary: string;
  content: string;
  tagsText: string;
  priority: string;
  isActive: boolean;
}

interface PropertyKnowledgeFormProps {
  heading: string;
  submitLabel: string;
  initialValue: PropertyKnowledgeFormDraft;
  isSaving: boolean;
  error: string | null;
  showApprovalNotice?: boolean;
  onSubmit: (value: PropertyKnowledgeFormSubmission) => Promise<void>;
  onCancel: () => void;
}

const titleLimit = 200;
const summaryLimit = 280;
const contentLimit = 6000;
const maxPriority = 10;
const maxTags = 12;
const maxTagLength = 40;

export function PropertyKnowledgeForm({
  heading,
  submitLabel,
  initialValue,
  isSaving,
  error,
  showApprovalNotice = false,
  onSubmit,
  onCancel
}: PropertyKnowledgeFormProps) {
  const [draft, setDraft] = useState(initialValue);
  const [validationError, setValidationError] = useState<string | null>(null);

  useEffect(() => {
    setDraft(initialValue);
    setValidationError(null);
  }, [initialValue]);

  const titleCount = draft.title.length;
  const summaryCount = draft.summary.length;
  const contentCount = draft.content.length;

  const tags = useMemo(() => normalizeTags(draft.tagsText), [draft.tagsText]);

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const normalizedPriority = Number.parseInt(draft.priority, 10);
    const title = draft.title.trim();
    const summary = draft.summary.trim();
    const content = draft.content.trim();

    const validation = validateDraft(title, summary, content, tags, normalizedPriority);
    if (validation) {
      setValidationError(validation);
      return;
    }

    setValidationError(null);
    await onSubmit({
      category: draft.category,
      title,
      summary,
      content,
      tags,
      priority: normalizedPriority,
      isActive: draft.isActive
    });
  }

  return (
    <form className="sf-knowledge-form" onSubmit={handleSubmit}>
      <div className="sf-knowledge-form-header">
        <div>
          <p className="sf-knowledge-kicker">Property Knowledge</p>
          <h3>{heading}</h3>
        </div>
        <button type="button" onClick={onCancel} aria-label="Cancel knowledge form">Cancel</button>
      </div>

      {showApprovalNotice ? (
        <div className="sf-knowledge-notice" role="note">
          Editing this approved item will require reapproval.
        </div>
      ) : null}

      {validationError ? <div className="sf-knowledge-error" role="alert">{validationError}</div> : null}
      {error ? <div className="sf-knowledge-error" role="alert">{error}</div> : null}

      <label>
        Category
        <select
          value={String(draft.category)}
          onChange={(event) => setDraft((current) => ({ ...current, category: Number(event.target.value) as PropertyKnowledgeCategory }))}
          disabled={isSaving}
        >
          {propertyKnowledgeCategoryOptions.map((option) => (
            <option key={option.value} value={option.value}>{propertyKnowledgeCategoryLabels[option.value]}</option>
          ))}
        </select>
      </label>

      <label>
        Title
        <input
          type="text"
          value={draft.title}
          maxLength={titleLimit}
          onChange={(event) => setDraft((current) => ({ ...current, title: event.target.value }))}
          disabled={isSaving}
          required
        />
        <span className="sf-knowledge-counter">{titleCount}/{titleLimit}</span>
      </label>

      <label>
        Summary
        <textarea
          value={draft.summary}
          maxLength={summaryLimit}
          onChange={(event) => setDraft((current) => ({ ...current, summary: event.target.value }))}
          rows={3}
          disabled={isSaving}
        />
        <span className="sf-knowledge-counter">{summaryCount}/{summaryLimit}</span>
      </label>

      <label>
        Content
        <textarea
          value={draft.content}
          maxLength={contentLimit}
          onChange={(event) => setDraft((current) => ({ ...current, content: event.target.value }))}
          rows={8}
          disabled={isSaving}
          required
        />
        <span className="sf-knowledge-counter">{contentCount}/{contentLimit}</span>
      </label>

      <label>
        Tags
        <input
          type="text"
          value={draft.tagsText}
          onChange={(event) => setDraft((current) => ({ ...current, tagsText: event.target.value }))}
          placeholder="wifi, parking, arrival"
          disabled={isSaving}
        />
        <span className="sf-knowledge-help">Comma-separated. Saved tags are normalized.</span>
        <span className="sf-knowledge-counter">{tags.length}/{maxTags}</span>
      </label>

      <label>
        Priority
        <input
          type="number"
          min={0}
          max={maxPriority}
          step={1}
          value={draft.priority}
          onChange={(event) => setDraft((current) => ({ ...current, priority: event.target.value }))}
          disabled={isSaving}
          required
        />
        <span className="sf-knowledge-help">Higher values rank earlier in AI grounding.</span>
      </label>

      <label className="sf-knowledge-checkbox-row">
        <input
          type="checkbox"
          checked={draft.isActive}
          onChange={(event) => setDraft((current) => ({ ...current, isActive: event.target.checked }))}
          disabled={isSaving}
        />
        Active
      </label>

      <div className="sf-knowledge-form-actions">
        <button type="submit" disabled={isSaving}>{isSaving ? `${submitLabel}...` : submitLabel}</button>
        <button type="button" onClick={onCancel} disabled={isSaving}>Cancel</button>
      </div>
    </form>
  );
}

export function createDraftFromValue(value: {
  category: PropertyKnowledgeCategory;
  title?: string | null;
  summary?: string | null;
  content?: string | null;
  tags?: string[];
  priority?: number;
  isActive?: boolean;
}): PropertyKnowledgeFormDraft {
  return {
    category: value.category,
    title: value.title ?? "",
    summary: value.summary ?? "",
    content: value.content ?? "",
    tagsText: (value.tags ?? []).join(", "),
    priority: String(value.priority ?? 0),
    isActive: value.isActive ?? true
  };
}

function validateDraft(
  title: string,
  summary: string,
  content: string,
  tags: string[],
  priority: number
): string | null {
  if (!title) {
    return "Knowledge title is required.";
  }

  if (title.length > titleLimit) {
    return `Knowledge title must be ${titleLimit} characters or fewer.`;
  }

  if (summary.length > summaryLimit) {
    return `Knowledge summary must be ${summaryLimit} characters or fewer.`;
  }

  if (!content) {
    return "Knowledge content is required.";
  }

  if (content.length > contentLimit) {
    return `Knowledge content must be ${contentLimit} characters or fewer.`;
  }

  if (!Number.isInteger(priority) || priority < 0 || priority > maxPriority) {
    return `Knowledge priority must be between 0 and ${maxPriority}.`;
  }

  if (tags.length > maxTags) {
    return `Knowledge tags must contain at most ${maxTags} values.`;
  }

  if (tags.some((tag) => tag.length > maxTagLength)) {
    return `Each knowledge tag must be ${maxTagLength} characters or fewer.`;
  }

  return null;
}

function normalizeTags(tagsText: string): string[] {
  return Array.from(new Set(
    tagsText
      .split(",")
      .map((tag) => tag.trim().toLowerCase().replace(/\s+/g, "-"))
      .filter((tag) => tag.length > 0)
  )).sort((left, right) => left.localeCompare(right));
}
