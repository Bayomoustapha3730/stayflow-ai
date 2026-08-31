import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { StayFlowChatWidget } from "../src/components";
import {
  ConversationMessageType,
  ConversationSenderType,
  ConversationStatus,
  GuestChannel
} from "../src/models/enums";

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

function messageResponse(
  content = "Check-in is 3:00 PM and check-out is 10:00 AM."
) {
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

function paymentProposalResponse() {
  return apiResponse({
    conversationId: "11111111-1111-1111-1111-111111111111",
    conversationStatus: ConversationStatus.Open,
    guestMessage: {
      id: "payment-guest-message",
      conversationId: "11111111-1111-1111-1111-111111111111",
      senderType: ConversationSenderType.Guest,
      content: "Send me an M-PESA request.",
      messageType: ConversationMessageType.Text,
      sentAt: "2026-01-01T10:00:00Z"
    },
    assistantMessage: {
      id: "payment-proposal",
      conversationId: "11111111-1111-1111-1111-111111111111",
      senderType: ConversationSenderType.AI,
      content: "I can send a payment request for 3000.00 KES to +2******0002. Should I send it?",
      messageType: ConversationMessageType.Text,
      sentAt: "2026-01-01T10:00:01Z"
    },
    humanTakeoverEnabled: false,
    requiresHostAttention: false,
    escalationReason: null,
    providerMetadata: null,
    pendingAction: {
      actionId: "payment-action",
      actionType: 8,
      status: 0,
      confirmationRequirement: 1,
      prompt: "I can send a payment request for 3000.00 KES to +2******0002. Should I send it?",
      requiresHostApproval: false,
      expiresAt: "2026-01-01T10:05:00Z"
    },
    createdAt: "2026-01-01T10:00:01Z"
  });
}

function paymentConfirmationResponse() {
  return apiResponse({
    conversationId: "11111111-1111-1111-1111-111111111111",
    conversationStatus: ConversationStatus.Open,
    guestMessage: {
      id: "payment-confirmation-event",
      conversationId: "11111111-1111-1111-1111-111111111111",
      senderType: ConversationSenderType.System,
      content: "Guest confirmed the pending action.",
      messageType: ConversationMessageType.InternalNote,
      sentAt: "2026-01-01T10:00:02Z"
    },
    assistantMessage: {
      id: "payment-completion",
      conversationId: "11111111-1111-1111-1111-111111111111",
      senderType: ConversationSenderType.AI,
      content: "I've sent the M-PESA payment request to your phone. Please confirm it on your device.",
      messageType: ConversationMessageType.Text,
      sentAt: "2026-01-01T10:00:03Z"
    },
    humanTakeoverEnabled: false,
    requiresHostAttention: false,
    escalationReason: null,
    providerMetadata: null,
    pendingAction: null,
    createdAt: "2026-01-01T10:00:03Z"
  });
}

function conversationResponse() {
  return apiResponse({
    conversationId: "11111111-1111-1111-1111-111111111111",
    status: ConversationStatus.Open,
    channel: GuestChannel.Web,
    subject: null,
    humanTakeoverEnabled: false,
    requiresHostAttention: false,
    startedAt: "2026-01-01T10:00:00Z",
    lastActivityAt: "2026-01-01T10:00:01Z",
    closedAt: null,
    reservation: null,
    recentMessages: []
  });
}

function historyResponse() {
  return apiResponse({
    conversationId: "11111111-1111-1111-1111-111111111111",
    messages: {
      items: [],
      pageNumber: 1,
      pageSize: 20,
      totalCount: 0,
      totalPages: 0
    }
  });
}

async function openAndLogin(fetchMock: ReturnType<typeof vi.fn>) {
  vi.stubGlobal("fetch", fetchMock);

  sessionStorage.clear();
  localStorage.clear();

  const user = userEvent.setup();

  render(
    <StayFlowChatWidget
      guestId={guestId}
      apiBaseUrl="http://localhost:5243"
      demoEmail="demo.user@stayflow.local"
    />
  );

  const openButton = screen.queryByRole("button", {
    name: /open stayflow chat/i
  });

  if (openButton) {
    await user.click(openButton);
  }

  await user.clear(screen.getByLabelText(/email/i));
  await user.type(
    screen.getByLabelText(/email/i),
    "guest@example.com"
  );
  await user.type(
    screen.getByLabelText(/password/i),
    "Password123!"
  );
  await user.click(
    screen.getByRole("button", { name: /sign in/i })
  );

  await waitFor(() =>
    expect(
      screen.getByText(/how can i help/i)
    ).toBeInTheDocument()
  );

  return user;
}

describe("StayFlowChatWidget", () => {
  it("opens, authenticates, and sends a protected web chat message", async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce(authResponse())
      .mockResolvedValueOnce(messageResponse())
      .mockResolvedValueOnce(conversationResponse());

    const user = await openAndLogin(fetchMock);

    const input = screen.getByRole("textbox", {
      name: /^message$/i
    });

    await user.type(
      input,
      "What are my check-in and check-out dates?"
    );

    await user.click(
      screen.getByRole("button", { name: /send message/i })
    );

    await screen.findByText(/check-in is 3:00 pm/i);

    const chatMessageCall = fetchMock.mock.calls.find(
      ([url, options]) =>
      /*  url === "http://localhost:5243/chat/message" */
      new URL(url).pathname === "/chat/message"
      &&
        options?.method === "POST"
    );

    expect(chatMessageCall).toBeDefined();

    const [, chatMessageOptions] = chatMessageCall!;

    expect(chatMessageOptions).toEqual(
      expect.objectContaining({
        method: "POST",
        headers: expect.objectContaining({
          Authorization: "Bearer access-token"
        }),
        body: expect.stringContaining(
          `"guestId":"${guestId}"`
        )
      })
    );

    const requestBody = JSON.parse(
      chatMessageOptions.body as string
    );

    expect(requestBody.channel).toBe(GuestChannel.Web);
  });

  it("does not submit blank messages", async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce(authResponse());

    const user = await openAndLogin(fetchMock);

    const sendButton = screen.getByRole("button", {
      name: /send message/i
    });

    expect(sendButton).toBeDisabled();

    await user.click(sendButton);

    expect(fetchMock).toHaveBeenCalledTimes(1);
  });

  it("confirms a payment action once without rendering action text as guest input", async () => {
    const fetchMock = vi.fn((url: string, _options?: RequestInit) => {
      const path = new URL(url).pathname;
      if (path === "/auth/login") {
        return Promise.resolve(authResponse());
      }

      if (path === "/chat/message") {
        return Promise.resolve(paymentProposalResponse());
      }

      if (path.endsWith("/actions/payment-action/confirm")) {
        return Promise.resolve(paymentConfirmationResponse());
      }

      if (path.endsWith("/history")) {
        return Promise.resolve(historyResponse());
      }

      if (path.endsWith("/read")) {
        return Promise.resolve(apiResponse(true));
      }

      return Promise.resolve(conversationResponse());
    });

    const user = await openAndLogin(fetchMock);
    const input = screen.getByRole("textbox", { name: /^message$/i });

    await user.type(input, "Send me an M-PESA request.");
    await user.click(screen.getByRole("button", { name: /send message/i }));

    const confirmButton = await screen.findByRole("button", { name: /^confirm$/i });
    expect(screen.getAllByText(/i can send a payment request for 3000\.00 kes/i)).toHaveLength(2);

    await user.click(confirmButton);

    await screen.findByText(/i've sent the m-pesa payment request/i);
    await waitFor(() => expect(screen.queryByRole("button", { name: /^confirm$/i })).not.toBeInTheDocument());
    expect(screen.queryByRole("button", { name: /^cancel$/i })).not.toBeInTheDocument();
    expect(screen.getAllByText("Send me an M-PESA request.")).toHaveLength(1);
    expect(screen.queryByText(/^I can send a payment request for 3000\.00 KES\.$/i)).not.toBeInTheDocument();

    const confirmationCall = fetchMock.mock.calls.find(
      ([url, options]) => new URL(url).pathname.endsWith("/actions/payment-action/confirm") && options?.method === "POST"
    );
    expect(confirmationCall).toBeDefined();
    expect(fetchMock.mock.calls.filter(([url]) => new URL(url).pathname.endsWith("/actions/payment-action/confirm"))).toHaveLength(1);
  });

  it("keeps shift enter as a draft newline and enter sends", async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce(authResponse())
      .mockResolvedValueOnce(messageResponse("Done."))
      .mockResolvedValueOnce(conversationResponse());

    const user = await openAndLogin(fetchMock);

    const input = screen.getByRole("textbox", {
      name: /^message$/i
    });

    await user.type(
      input,
      "Line one{Shift>}{Enter}{/Shift}Line two"
    );

    expect(input).toHaveValue("Line one\nLine two");

    await user.keyboard("{Enter}");

    await screen.findByText("Done.");

    const chatMessageCalls = fetchMock.mock.calls.filter(
      ([url, options]) =>
        //url === "http://localhost:5243/chat/message" 
      new URL(url).pathname === "/chat/message"
      &&
        options?.method === "POST"
    );

    expect(chatMessageCalls).toHaveLength(1);
  });

  it("shows host attention state after escalation", async () => {
    const guestSafeMessage = "A host will help with your request shortly.";
    const escalated = messageResponse("I am connecting you with a host.");
    const originalJson = escalated.json;

    escalated.json = async () => {
      const payload = await originalJson();

      payload.data.conversationStatus =
        ConversationStatus.Escalated;
      payload.data.requiresHostAttention = true;

      return payload;
    };

    const escalatedConversation = apiResponse({
      conversationId: "11111111-1111-1111-1111-111111111111",
      status: ConversationStatus.Escalated,
      channel: GuestChannel.Web,
      subject: null,
      humanTakeoverEnabled: false,
      requiresHostAttention: true,
      guestSafeMessage,
      startedAt: "2026-01-01T10:00:00Z",
      lastActivityAt: "2026-01-01T10:00:01Z",
      closedAt: null,
      reservation: null,
      recentMessages: []
    });

    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce(authResponse())
      .mockResolvedValueOnce(escalated)
      .mockResolvedValueOnce(escalatedConversation);

    const user = await openAndLogin(fetchMock);

    const input = screen.getByRole("textbox", {
      name: /^message$/i
    });

    await user.type(input, "Can someone help?");

    await user.click(
      screen.getByRole("button", { name: /send message/i })
    );

    expect(await screen.findByText(guestSafeMessage)).toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: /host already notified/i })
    ).toBeDisabled();

    expect(input).toBeEnabled();

    expect(screen.getByText(guestSafeMessage)).toHaveClass("sf-chat-status");
  });

  it("submits assistant message feedback", async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce(authResponse())
      .mockResolvedValueOnce(messageResponse("Here is your answer."))
      .mockResolvedValueOnce(conversationResponse())
      .mockResolvedValueOnce(historyResponse())
      .mockResolvedValueOnce(apiResponse({
        id: "feedback-1",
        conversationId: "11111111-1111-1111-1111-111111111111",
        conversationMessageId: "assistant-message",
        guestId,
        feedbackValue: 0,
        comment: null,
        submittedAt: "2026-01-01T10:00:02Z"
      }));

    const user = await openAndLogin(fetchMock);

    const input = screen.getByRole("textbox", {
      name: /^message$/i
    });

    await user.type(input, "Need check-in details");
    await user.click(screen.getByRole("button", { name: /send message/i }));
    await screen.findByText("Here is your answer.");

    await user.click(screen.getByRole("button", { name: /^helpful$/i }));

    await waitFor(() => {
      const feedbackCall = fetchMock.mock.calls.find(
        ([url, options]) =>
          new URL(url).pathname === "/chat/11111111-1111-1111-1111-111111111111/messages/assistant-message/feedback"
          && options?.method === "POST"
      );

      expect(feedbackCall).toBeDefined();
    });
  });
});