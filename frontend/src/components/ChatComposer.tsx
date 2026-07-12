import { KeyboardEvent, useState } from "react";

interface ChatComposerProps {
  disabled: boolean;
  isSending: boolean;
  onSend: (message: string) => Promise<boolean>;
}

export function ChatComposer({ disabled, isSending, onSend }: ChatComposerProps) {
  const [draft, setDraft] = useState("");
  const canSend = draft.trim().length > 0 && !disabled && !isSending;

  async function submit() {
    if (!canSend) {
      return;
    }

    const sent = await onSend(draft);
    if (sent) {
      setDraft("");
    }
  }

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
        onChange={(event) => setDraft(event.target.value)}
        onKeyDown={handleKeyDown}
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
