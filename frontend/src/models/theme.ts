export type ChatButtonPosition = "bottom-right" | "bottom-left";

export interface StayFlowChatTheme {
  assistantName: string;
  welcomeMessage: string;
  logoUrl?: string;
  primaryColor: string;
  accentColor: string;
  backgroundColor: string;
  textColor: string;
  guestBubbleColor: string;
  assistantBubbleColor: string;
  buttonPosition: ChatButtonPosition;
  borderRadius: string;
  propertyDisplayName?: string;
  locale: string;
}

export const defaultChatTheme: StayFlowChatTheme = {
  assistantName: "StayFlow Concierge",
  welcomeMessage: "Hi! How can I help with your stay?",
  primaryColor: "#123B5D",
  accentColor: "#2D8CFF",
  backgroundColor: "#FFFFFF",
  textColor: "#17202A",
  guestBubbleColor: "#123B5D",
  assistantBubbleColor: "#F1F5F9",
  buttonPosition: "bottom-right",
  borderRadius: "18px",
  locale: "en-KE"
};
