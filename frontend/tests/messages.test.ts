import { describe, expect, it } from "vitest";
import { ConversationMessageType, ConversationSenderType } from "../src/models/enums";
import type { ChatMessage } from "../src/models/chat";
import { mergeMessages } from "../src/utils/messages";

function message(id: string, sentAt: string, content = id): ChatMessage {
  return {
    id,
    conversationId: "conversation-1",
    senderType: ConversationSenderType.AI,
    content,
    messageType: ConversationMessageType.Text,
    sentAt
  };
}

describe("message utilities", () => {
  it("sorts and de-duplicates visible conversation messages", () => {
    const merged = mergeMessages(
      [message("later", "2026-01-01T12:00:00Z"), message("same", "2026-01-01T11:00:00Z", "old")],
      [message("earlier", "2026-01-01T10:00:00Z"), message("same", "2026-01-01T11:00:00Z", "new")]
    );

    expect(merged.map((item) => item.id)).toEqual(["earlier", "same", "later"]);
    expect(merged.find((item) => item.id === "same")?.content).toBe("new");
  });

  it("filters internal notes from guest-visible history", () => {
    const internalNote = {
      ...message("note", "2026-01-01T09:00:00Z"),
      messageType: ConversationMessageType.InternalNote
    };

    expect(mergeMessages([], [internalNote, message("visible", "2026-01-01T10:00:00Z")])).toHaveLength(1);
  });

  it("keeps an assistant action proposal from being rendered as a duplicate guest message", () => {
    const proposal = "I can send a payment request for 3000.00 KES. Should I send it?";
    const merged = mergeMessages([], [
      {
        ...message("guest-request", "2026-01-01T10:00:00Z", "Send me an M-PESA request."),
        senderType: ConversationSenderType.Guest
      },
      message("assistant-proposal", "2026-01-01T10:00:01Z", proposal),
      {
        ...message("historical-guest-copy", "2026-01-01T10:00:02Z", proposal),
        senderType: ConversationSenderType.Guest
      },
      message(
        "assistant-completion",
        "2026-01-01T10:00:03Z",
        "I've sent the M-PESA payment request to your phone. Please confirm it on your device."
      )
    ]);

    expect(merged.filter((item) => item.senderType === ConversationSenderType.Guest)).toHaveLength(1);
    expect(merged.find((item) => item.id === "guest-request")?.content).toBe("Send me an M-PESA request.");
    expect(merged.some((item) => item.id === "historical-guest-copy")).toBe(false);
    expect(merged.find((item) => item.id === "assistant-proposal")?.senderType).toBe(ConversationSenderType.AI);
    expect(merged.find((item) => item.id === "assistant-completion")?.senderType).toBe(ConversationSenderType.AI);
  });
});
