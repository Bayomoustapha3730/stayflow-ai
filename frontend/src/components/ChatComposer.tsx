import { KeyboardEvent, useEffect, useRef, useState } from "react";

interface ChatComposerProps {
  disabled: boolean;
  isSending: boolean;
  onSend: (message: string) => Promise<boolean>;
  onStartTyping?: () => void;
  onStopTyping?: () => void;
}

export function ChatComposer({ disabled, isSending, onSend, onStartTyping, onStopTyping }: ChatComposerProps) {
  const [draft, setDraft] = useState("");
  const isTypingRef = useRef(false);
  const stopTypingTimerRef = useRef<number | null>(null);
  const canSend = draft.trim().length > 0 && !disabled && !isSending;

  async function submit() {
    if (!canSend) {
      return;
    }

    const sent = await onSend(draft);
    if (sent) {
      setDraft("");
      if (isTypingRef.current) {
        isTypingRef.current = false;
        onStopTyping?.();
      }
    }
  }

  function emitTyping(nextDraft: string) {
    if (!onStartTyping || !onStopTyping) {
      return;
    }

    if (nextDraft.trim().length > 0 && !isTypingRef.current) {
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

  function handleKeyDown(event: KeyboardEvent<HTMLTextAreaElement>) {
    if (event.key === "Enter" && !event.shiftKey) {
      event.preventDefault();
      void submit();
    }
  }

  return (
    <form
      className="sf-chat-composer"
      onSubmit={(event) => {
        event.preventDefault();
        void submit();
      }}
    >
      <label className="sf-chat-sr-only" htmlFor="sf-chat-message-input">
        Message
      </label>
      <textarea
        id="sf-chat-message-input"
        value={draft}
        onChange={(event) => {
          setDraft(event.target.value);
          emitTyping(event.target.value);
        }}
        onKeyDown={handleKeyDown}
        onBlur={() => {
          if (isTypingRef.current) {
            isTypingRef.current = false;
            onStopTyping?.();
          }
        }}
        placeholder={disabled ? "Conversation is not accepting messages" : "Ask about your stay..."}
        disabled={disabled}
        rows={2}
        maxLength={2000}
      />
      <button type="submit" disabled={!canSend} aria-label="Send message">
        {isSending ? "Sending" : "Send"}
      </button>
    </form>
  );
}
