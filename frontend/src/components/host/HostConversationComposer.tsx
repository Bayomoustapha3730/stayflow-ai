import { KeyboardEvent, useState } from "react";

const maxMessageLength = 2000;

interface HostConversationComposerProps {
  isSending: boolean;
  disabled: boolean;
  disabledReason?: string;
  actionError: string | null;
  onSend: (content: string) => Promise<boolean>;
}

export function HostConversationComposer({
  isSending,
  disabled,
  disabledReason,
  actionError,
  onSend
}: HostConversationComposerProps) {
  const [content, setContent] = useState("");

  const trimmed = content.trim();
  const isSendDisabled = disabled || isSending || trimmed.length === 0 || trimmed.length > maxMessageLength;

  async function submit() {
    if (isSendDisabled) {
      return;
    }

    const sent = await onSend(trimmed);
    if (sent) {
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
    <section className="sf-host-detail-section" aria-label="Host reply composer">
      <h3>Reply to guest</h3>
      {disabledReason ? <p className="sf-host-muted-note">{disabledReason}</p> : null}

      <label htmlFor="sf-host-reply-input">Host reply</label>
      <textarea
        id="sf-host-reply-input"
        value={content}
        onChange={(event) => setContent(event.target.value)}
        onKeyDown={handleKeyDown}
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
