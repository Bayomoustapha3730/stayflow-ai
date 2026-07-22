import { KeyboardEvent, useState } from "react";

const maxNoteLength = 2000;

interface HostInternalNoteComposerProps {
  isAddingNote: boolean;
  disabled: boolean;
  actionError: string | null;
  onSubmit: (content: string) => Promise<boolean>;
}

export function HostInternalNoteComposer({
  isAddingNote,
  disabled,
  actionError,
  onSubmit
}: HostInternalNoteComposerProps) {
  const [content, setContent] = useState("");

  const trimmed = content.trim();
  const isDisabled = disabled || isAddingNote || trimmed.length === 0 || trimmed.length > maxNoteLength;

  async function submit() {
    if (isDisabled) {
      return;
    }

    const added = await onSubmit(trimmed);
    if (added) {
      setContent("");
    }
  }

  async function handleKeyDown(event: KeyboardEvent<HTMLTextAreaElement>) {
    if ((event.metaKey || event.ctrlKey) && event.key === "Enter") {
      event.preventDefault();
      await submit();
    }
  }

  return (
    <section className="sf-host-detail-section sf-host-note-composer" aria-label="Internal notes">
      <h3>Internal note</h3>
      <p className="sf-host-muted-note">Internal note - staff only</p>

      <label htmlFor="sf-host-note-input">Note content</label>
      <textarea
        id="sf-host-note-input"
        value={content}
        onChange={(event) => setContent(event.target.value)}
        onKeyDown={handleKeyDown}
        rows={3}
        maxLength={maxNoteLength + 1}
        disabled={disabled}
        placeholder={disabled ? "Notes unavailable for this conversation" : "Add context for staff teammates"}
      />

      <div className="sf-host-composer-footer">
        <span aria-live="polite">{trimmed.length}/{maxNoteLength}</span>
        <button type="button" onClick={() => void submit()} disabled={isDisabled} aria-label="Add internal note">
          {isAddingNote ? "Adding..." : "Add Note"}
        </button>
      </div>

      {actionError ? <p className="sf-host-inline-error">{actionError}</p> : null}
    </section>
  );
}
