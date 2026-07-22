import { KeyboardEvent, useEffect, useRef, useState } from "react";

const maxMessageLength = 2000;

interface HostConversationComposerProps {
  isSending: boolean;
  disabled: boolean;
  disabledReason?: string;
  actionError: string | null;
  externalDraft?: string | null;
  externalDraftVersion?: number;
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
  onSend,
  onStartTyping,
  onStopTyping
}: HostConversationComposerProps) {
  const [content, setContent] = useState("");
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
  }, [externalDraft, externalDraftVersion]);

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

      <label htmlFor="sf-host-reply-input">Host reply</label>
      <textarea
        id="sf-host-reply-input"
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
        rows={4}
        maxLength={maxMessageLength + 1}
        disabled={disabled}
        placeholder={disabled ? "Reply is currently unavailable" : "Type a reply to the guest"}
      />

      <div className="sf-host-composer-footer">
        <span aria-live="polite">{trimmed.length}/{maxMessageLength}</span>
        <button type="button" onClick={() => void submit()} disabled={isSendDisabled} aria-label="Send host reply">
          {isSending ? "Sending..." : "Send Reply"}
        </button>
      </div>

      {actionError ? <p className="sf-host-inline-error">{actionError}</p> : null}
    </section>
  );
}
