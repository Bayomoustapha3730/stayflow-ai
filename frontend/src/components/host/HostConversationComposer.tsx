import { KeyboardEvent, useEffect, useRef, useState } from "react";

const maxMessageLength = 2000;

interface HostConversationComposerProps {
  isSending: boolean;
  disabled: boolean;
  disabledReason?: string;
  actionError: string | null;
  externalDraft?: string | null;
  externalDraftVersion?: number;
  requestFocusVersion?: number;
  onDraftChange?: (draft: string) => void;
  onSend: (content: string) => Promise<boolean>;
  onStartTyping?: () => void;
  onStopTyping?: () => void;
}

export function HostConversationComposer({
  isSending,
  disabled,
  disabledReason,
  actionError,
  externalDraft,
  externalDraftVersion,
  requestFocusVersion,
  onDraftChange,
  onSend,
  onStartTyping,
  onStopTyping
}: HostConversationComposerProps) {
  const [content, setContent] = useState("");
  const textareaRef = useRef<HTMLTextAreaElement | null>(null);
  const stopTypingTimerRef = useRef<number | null>(null);
  const isTypingRef = useRef(false);

  const trimmed = content.trim();
  const isSendDisabled = disabled || isSending || trimmed.length === 0 || trimmed.length > maxMessageLength;

  async function submit() {
    if (isSendDisabled) {
      return;
    }

    const sent = await onSend(trimmed);
    if (sent) {
      setContent("");
      onDraftChange?.("");
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

  useEffect(() => {
    if (!externalDraft) {
      return;
    }

    setContent(externalDraft);
    onDraftChange?.(externalDraft);
  }, [externalDraft, externalDraftVersion, onDraftChange]);

  useEffect(() => {
    if (!requestFocusVersion) {
      return;
    }

    textareaRef.current?.focus();
  }, [requestFocusVersion]);

  async function handleKeyDown(event: KeyboardEvent<HTMLTextAreaElement>) {
    if ((event.metaKey || event.ctrlKey) && event.key === "Enter") {
      event.preventDefault();
      await submit();
    }
  }

  return (
    <section className="sf-host-detail-section" aria-label="Host reply composer">
      <h3>Reply to guest</h3>
      {disabledReason ? <p className="sf-host-muted-note">{disabledReason}</p> : null}

      <label htmlFor="sf-host-reply-input">Reply to guest</label>
      <textarea
        id="sf-host-reply-input"
        ref={textareaRef}
        value={content}
        onChange={(event) => {
          setContent(event.target.value);
          onDraftChange?.(event.target.value);
          emitTypingState(event.target.value);
        }}
        onKeyDown={handleKeyDown}
        onBlur={() => {
          if (isTypingRef.current) {
            isTypingRef.current = false;
            onStopTyping?.();
          }
        }}
        rows={4}
        maxLength={maxMessageLength + 1}
        disabled={disabled}
        placeholder={disabled ? "Reply is currently unavailable" : "Type a reply to the guest"}
      />

      <div className="sf-host-composer-footer">
        <span aria-live="polite">{trimmed.length}/{maxMessageLength}</span>
        <button
          type="button"
          className="sf-host-button-primary"
          onClick={() => void submit()}
          disabled={isSendDisabled}
          aria-label="Send Reply"
        >
          {isSending ? "Sending..." : "Send Reply"}
        </button>
      </div>

      {actionError ? <p className="sf-host-inline-error" role="alert">{actionError}</p> : null}
    </section>
  );
}
