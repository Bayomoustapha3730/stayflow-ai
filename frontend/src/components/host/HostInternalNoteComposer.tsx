import { KeyboardEvent, useEffect, useRef, useState } from "react";

const maxNoteLength = 2000;

interface HostInternalNoteComposerProps {
  isAddingNote: boolean;
  disabled: boolean;
  disabledReason?: string;
  actionError: string | null;
  onSubmit: (content: string) => Promise<boolean>;
  onStartTyping?: () => void;
  onStopTyping?: () => void;
}

export function HostInternalNoteComposer({
  isAddingNote,
  disabled,
  disabledReason,
  actionError,
  onSubmit,
  onStartTyping,
  onStopTyping
}: HostInternalNoteComposerProps) {
  const [content, setContent] = useState("");
  const stopTypingTimerRef = useRef<number | null>(null);
  const isTypingRef = useRef(false);

  const trimmed = content.trim();
  const isDisabled = disabled || isAddingNote || trimmed.length === 0 || trimmed.length > maxNoteLength;

  async function submit() {
    if (isDisabled) {
      return;
    }

    const added = await onSubmit(trimmed);
    if (added) {
      setContent("");
      if (isTypingRef.current) {
        isTypingRef.current = false;
        onStopTyping?.();
      }
    }
  }

  function emitTypingState(nextContent: string) {
    if (!onStartTyping || !onStopTyping) {
      return;
    }

    const hasContent = nextContent.trim().length > 0;

    if (hasContent && !isTypingRef.current) {
      isTypingRef.current = true;
      onStartTyping();
    }

    if (stopTypingTimerRef.current) {
      window.clearTimeout(stopTypingTimerRef.current);
    }

    stopTypingTimerRef.current = window.setTimeout(() => {
      if (isTypingRef.current) {
        isTypingRef.current = false;
        onStopTyping();
      }
    }, 1200);
  }

  useEffect(() => {
    return () => {
      if (stopTypingTimerRef.current) {
        window.clearTimeout(stopTypingTimerRef.current);
      }

      if (isTypingRef.current) {
        onStopTyping?.();
      }
    };
  }, [onStopTyping]);

  async function handleKeyDown(event: KeyboardEvent<HTMLTextAreaElement>) {
    if ((event.metaKey || event.ctrlKey) && event.key === "Enter") {
      event.preventDefault();
      await submit();
    }
  }

  return (
    <section className="sf-host-detail-section sf-host-note-composer" aria-label="Internal notes">
      <h3>Internal note</h3>
      <p className="sf-host-muted-note">Staff only — not visible to the guest</p>
      {disabledReason ? <p className="sf-host-muted-note">{disabledReason}</p> : null}

      <label htmlFor="sf-host-note-input">Note content</label>
      <textarea
        id="sf-host-note-input"
        value={content}
        onChange={(event) => {
          setContent(event.target.value);
          emitTypingState(event.target.value);
        }}
        onKeyDown={handleKeyDown}
        onBlur={() => {
          if (isTypingRef.current) {
            isTypingRef.current = false;
            onStopTyping?.();
          }
        }}
        rows={3}
        maxLength={maxNoteLength + 1}
        disabled={disabled}
        placeholder={disabled ? "Notes unavailable for this conversation" : "Add context for staff teammates"}
      />

      <div className="sf-host-composer-footer">
        <span aria-live="polite">{trimmed.length}/{maxNoteLength}</span>
        <button
          type="button"
          className="sf-host-button-note"
          onClick={() => void submit()}
          disabled={isDisabled}
          aria-label="Add Note"
        >
          {isAddingNote ? "Adding Note..." : "Add Note"}
        </button>
      </div>

      {actionError ? <p className="sf-host-inline-error" role="alert">{actionError}</p> : null}
    </section>
  );
}
