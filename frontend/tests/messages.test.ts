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
});
