interface EmptyConversationStateProps {
  welcomeMessage: string;
}

export function EmptyConversationState({ welcomeMessage }: EmptyConversationStateProps) {
  return (
    <div className="sf-chat-empty">
      <p>{welcomeMessage}</p>
      <div className="sf-chat-suggestions" aria-label="Suggested questions">
        <span>Wi-Fi information</span>
        <span>Check-in details</span>
        <span>House rules</span>
      </div>
    </div>
  );
}
