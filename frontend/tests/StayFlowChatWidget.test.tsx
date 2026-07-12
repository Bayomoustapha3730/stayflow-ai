import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { StayFlowChatWidget } from "../src/components";
import { ConversationMessageType, ConversationSenderType, ConversationStatus, GuestChannel } from "../src/models/enums";

const guestId = "44444444-4444-4444-4444-444444444444";

function apiResponse<T>(data: T) {
  return {
    ok: true,
    status: 200,
    json: async () => ({
      success: true,
      message: "ok",
      data,
      errors: [],
      correlationId: "correlation"
    })
  };
}

function authResponse() {
  return apiResponse({
    accessToken: "access-token",
    refreshToken: "refresh-token",
    expiresAt: "2026-01-01T00:30:00Z"
  });
}

function messageResponse(content = "Check-in is 3:00 PM and check-out is 10:00 AM.") {
  return apiResponse({
    conversationId: "11111111-1111-1111-1111-111111111111",
    conversationStatus: ConversationStatus.Open,
    guestMessage: {
      id: "guest-message",
      conversationId: "11111111-1111-1111-1111-111111111111",
      senderType: ConversationSenderType.Guest,
      content: "What are my check-in and check-out dates?",
      messageType: ConversationMessageType.Text,
      sentAt: "2026-01-01T10:00:00Z"
    },
    assistantMessage: {
      id: "assistant-message",
      conversationId: "11111111-1111-1111-1111-111111111111",
      senderType: ConversationSenderType.AI,
      content,
      messageType: ConversationMessageType.Text,
      sentAt: "2026-01-01T10:00:01Z"
    },
    humanTakeoverEnabled: false,
    requiresHostAttention: false,
    escalationReason: null,
    providerMetadata: null,
    createdAt: "2026-01-01T10:00:01Z"
  });
}

async function openAndLogin(fetchMock: ReturnType<typeof vi.fn>) {
  vi.stubGlobal("fetch", fetchMock);
  const user = userEvent.setup();
  render(<StayFlowChatWidget guestId={guestId} apiBaseUrl="http://localhost:5243" demoEmail="guest@example.com" />);

  await user.click(screen.getByRole("button", { name: /open stayflow chat/i }));
  await user.clear(screen.getByLabelText(/email/i));
  await user.type(screen.getByLabelText(/email/i), "guest@example.com");
  await user.type(screen.getByLabelText(/password/i), "Password123!");
  await user.click(screen.getByRole("button", { name: /sign in/i }));

  await waitFor(() => expect(screen.getByText(/how can i help/i)).toBeInTheDocument());
  return user;
}

describe("StayFlowChatWidget", () => {
  it("opens, authenticates, and sends a protected web chat message", async () => {
    const fetchMock = vi.fn().mockResolvedValueOnce(authResponse()).mockResolvedValueOnce(messageResponse());
    const user = await openAndLogin(fetchMock);

    await user.type(screen.getByLabelText(/message/i), "What are my check-in and check-out dates?");
    await user.click(screen.getByRole("button", { name: /send message/i }));

    await screen.findByText(/check-in is 3:00 pm/i);
    expect(fetchMock).toHaveBeenLastCalledWith(
      "http://localhost:5243/chat/message",
      expect.objectContaining({
        method: "POST",
        headers: expect.objectContaining({ Authorization: "Bearer access-token" }),
        body: expect.stringContaining(`"guestId":"${guestId}"`)
      })
    );
    expect(JSON.parse(fetchMock.mock.calls[1][1].body).channel).toBe(GuestChannel.Web);
  });

  it("does not submit blank messages", async () => {
    const fetchMock = vi.fn().mockResolvedValueOnce(authResponse());
    const user = await openAndLogin(fetchMock);

    await user.click(screen.getByRole("button", { name: /send message/i }));

    expect(fetchMock).toHaveBeenCalledTimes(1);
  });

  it("keeps shift enter as a draft newline and enter sends", async () => {
    const fetchMock = vi.fn().mockResolvedValueOnce(authResponse()).mockResolvedValueOnce(messageResponse("Done."));
    const user = await openAndLogin(fetchMock);
    const input = screen.getByLabelText(/message/i);

    await user.type(input, "Line one{Shift>}{Enter}{/Shift}Line two");
    expect(input).toHaveValue("Line one\nLine two");
    await user.keyboard("{Enter}");

    await screen.findByText("Done.");
    expect(fetchMock).toHaveBeenCalledTimes(2);
  });

  it("shows host attention state and disables new messages after escalation", async () => {
    const escalated = messageResponse("A host will help.");
    const originalJson = escalated.json;
    escalated.json = async () => {
      const payload = await originalJson();
      payload.data.conversationStatus = ConversationStatus.Escalated;
      payload.data.requiresHostAttention = true;
      return payload;
    };

    const fetchMock = vi.fn().mockResolvedValueOnce(authResponse()).mockResolvedValueOnce(escalated);
    const user = await openAndLogin(fetchMock);

    await user.type(screen.getByLabelText(/message/i), "Can someone help?");
    await user.click(screen.getByRole("button", { name: /send message/i }));

    await screen.findByText(/host has been notified/i);
    expect(screen.getByLabelText(/message/i)).toBeDisabled();
  });
});
