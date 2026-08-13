import { act, renderHook, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { buildConversationStorageKey, useChat } from "../src/hooks/useChat";
import { ConversationSenderType, ConversationStatus, GuestChannel } from "../src/models/enums";

const companyAScope = {
  apiBaseUrl: "http://test.local",
  guestId: "11111111-1111-4111-8111-111111111111",
  propertyId: "aaaaaaaa-1111-4111-8111-aaaaaaaaaaaa"
};

const companyBScope = {
  apiBaseUrl: "http://test.local",
  guestId: "22222222-2222-4222-8222-222222222222",
  propertyId: "bbbbbbbb-2222-4222-8222-bbbbbbbbbbbb"
};

const legacyConversationStorageKey = "stayflow.chat.conversationId";

function apiSuccess<T>(data: T) {
  return {
    ok: true,
    status: 200,
    headers: { get: () => null },
    json: async () => ({ success: true, message: "ok", data, errors: [], correlationId: "cid" })
  };
}

function chatMessage(conversationId: string, senderType: ConversationSenderType, content: string) {
  return {
    id: `${conversationId}-${senderType}-${content.length}`,
    conversationId,
    senderType,
    messageType: 0,
    content,
    isInternal: false,
    sentAt: "2026-08-12T10:00:00Z"
  };
}

function createChatFetchMock(conversationIdByProperty: Record<string, string>) {
  return vi.fn().mockImplementation((url: string, options?: RequestInit) => {
    if (url.endsWith("/chat/message")) {
      const body = JSON.parse(String(options?.body ?? "{}")) as { propertyId?: string; message: string };
      const conversationId = conversationIdByProperty[body.propertyId ?? ""] ?? "unknown-conversation";

      return Promise.resolve(
        apiSuccess({
          conversationId,
          conversationStatus: ConversationStatus.Open,
          humanTakeoverEnabled: false,
          requiresHostAttention: false,
          guestMessage: chatMessage(conversationId, ConversationSenderType.Guest, body.message),
          assistantMessage: chatMessage(conversationId, ConversationSenderType.AI, "Check-in is at 3:00 PM."),
          sources: [],
          warnings: []
        })
      );
    }

    return Promise.resolve(apiSuccess({}));
  });
}

describe("guest widget conversation scope isolation", () => {
  beforeEach(() => {
    sessionStorage.clear();
    vi.stubGlobal("crypto", { randomUUID: () => "00000000-0000-4000-8000-000000000001" });
  });

  it("builds distinct storage keys for different property and guest contexts", () => {
    expect(buildConversationStorageKey(companyAScope)).not.toEqual(buildConversationStorageKey(companyBScope));
  });

  it("builds a stable storage key for the same widget context", () => {
    expect(buildConversationStorageKey(companyAScope)).toEqual(buildConversationStorageKey({ ...companyAScope }));
  });

  it("scopes the key by api base url so separate deployments never share a conversation", () => {
    const other = buildConversationStorageKey({ ...companyAScope, apiBaseUrl: "http://other.local" });
    expect(other).not.toEqual(buildConversationStorageKey(companyAScope));
  });

  it("does not reuse a conversation id stored for another organization's widget context", () => {
    sessionStorage.setItem(buildConversationStorageKey(companyAScope), "conversation-a");

    const { result } = renderHook(() => useChat({ apiBaseUrl: companyBScope.apiBaseUrl, guestId: companyBScope.guestId, propertyId: companyBScope.propertyId }));

    expect(result.current.conversationId).toBeNull();
  });

  it("preserves conversation continuity for the same widget context", () => {
    sessionStorage.setItem(buildConversationStorageKey(companyAScope), "conversation-a");

    const { result } = renderHook(() => useChat({ apiBaseUrl: companyAScope.apiBaseUrl, guestId: companyAScope.guestId, propertyId: companyAScope.propertyId }));

    expect(result.current.conversationId).toBe("conversation-a");
  });

  it("discards a legacy unscoped conversation id", () => {
    sessionStorage.setItem(legacyConversationStorageKey, "legacy-conversation");

    const { result } = renderHook(() => useChat({ apiBaseUrl: companyAScope.apiBaseUrl, guestId: companyAScope.guestId, propertyId: companyAScope.propertyId }));

    expect(result.current.conversationId).toBeNull();
    expect(sessionStorage.getItem(legacyConversationStorageKey)).toBeNull();
  });

  it("persists the conversation id under the active widget scope only", async () => {
    vi.stubGlobal("fetch", createChatFetchMock({ [companyAScope.propertyId]: "conversation-a" }));

    const { result } = renderHook(() => useChat({ apiBaseUrl: companyAScope.apiBaseUrl, guestId: companyAScope.guestId, propertyId: companyAScope.propertyId }));

    await act(async () => {
      await result.current.sendMessage("What time is check-in?");
    });

    await waitFor(() => expect(result.current.conversationId).toBe("conversation-a"));
    expect(sessionStorage.getItem(buildConversationStorageKey(companyAScope))).toBe("conversation-a");
    expect(sessionStorage.getItem(buildConversationStorageKey(companyBScope))).toBeNull();
    expect(sessionStorage.getItem(legacyConversationStorageKey)).toBeNull();
  });

  it("resets conversation state and transcript when the widget context changes", async () => {
    vi.stubGlobal("fetch", createChatFetchMock({
      [companyAScope.propertyId]: "conversation-a",
      [companyBScope.propertyId]: "conversation-b"
    }));

    const { result, rerender } = renderHook((props: { guestId: string; propertyId: string }) =>
      useChat({ apiBaseUrl: companyAScope.apiBaseUrl, guestId: props.guestId, propertyId: props.propertyId }),
      { initialProps: { guestId: companyAScope.guestId, propertyId: companyAScope.propertyId } });

    await act(async () => {
      await result.current.sendMessage("What time is check-in?");
    });

    await waitFor(() => expect(result.current.conversationId).toBe("conversation-a"));
    expect(result.current.messages.length).toBeGreaterThan(0);

    rerender({ guestId: companyBScope.guestId, propertyId: companyBScope.propertyId });

    await waitFor(() => expect(result.current.conversationId).toBeNull());
    expect(result.current.messages).toHaveLength(0);

    await act(async () => {
      await result.current.sendMessage("What time is check-in?");
    });

    await waitFor(() => expect(result.current.conversationId).toBe("conversation-b"));
    expect(sessionStorage.getItem(buildConversationStorageKey(companyAScope))).toBe("conversation-a");
    expect(sessionStorage.getItem(buildConversationStorageKey(companyBScope))).toBe("conversation-b");
  });

  it("restores the original conversation when switching back to the first widget context", async () => {
    sessionStorage.setItem(buildConversationStorageKey(companyAScope), "conversation-a");
    sessionStorage.setItem(buildConversationStorageKey(companyBScope), "conversation-b");
    vi.stubGlobal("fetch", createChatFetchMock({}));

    const { result, rerender } = renderHook((props: { guestId: string; propertyId: string }) =>
      useChat({ apiBaseUrl: companyAScope.apiBaseUrl, guestId: props.guestId, propertyId: props.propertyId }),
      { initialProps: { guestId: companyAScope.guestId, propertyId: companyAScope.propertyId } });

    expect(result.current.conversationId).toBe("conversation-a");

    rerender({ guestId: companyBScope.guestId, propertyId: companyBScope.propertyId });
    await waitFor(() => expect(result.current.conversationId).toBe("conversation-b"));

    rerender({ guestId: companyAScope.guestId, propertyId: companyAScope.propertyId });
    await waitFor(() => expect(result.current.conversationId).toBe("conversation-a"));
  });

  it("clears only the active scope when starting a new conversation", () => {
    sessionStorage.setItem(buildConversationStorageKey(companyAScope), "conversation-a");
    sessionStorage.setItem(buildConversationStorageKey(companyBScope), "conversation-b");

    const { result } = renderHook(() => useChat({ apiBaseUrl: companyAScope.apiBaseUrl, guestId: companyAScope.guestId, propertyId: companyAScope.propertyId }));

    act(() => {
      result.current.startNewConversation();
    });

    expect(sessionStorage.getItem(buildConversationStorageKey(companyAScope))).toBeNull();
    expect(sessionStorage.getItem(buildConversationStorageKey(companyBScope))).toBe("conversation-b");
  });

  it("sends the guest channel and reservation context supplied by the widget", async () => {
    const fetchMock = createChatFetchMock({ [companyAScope.propertyId]: "conversation-a" });
    vi.stubGlobal("fetch", fetchMock);

    const { result } = renderHook(() => useChat({
      apiBaseUrl: companyAScope.apiBaseUrl,
      guestId: companyAScope.guestId,
      propertyId: companyAScope.propertyId,
      reservationId: "res-1"
    }));

    await act(async () => {
      await result.current.sendMessage("What time is check-in?");
    });

    const call = fetchMock.mock.calls.find(([url]) => String(url).endsWith("/chat/message"));
    const body = JSON.parse(String((call?.[1] as RequestInit).body));
    expect(body.channel).toBe(GuestChannel.Web);
    expect(body.propertyId).toBe(companyAScope.propertyId);
    expect(body.reservationId).toBe("res-1");
    expect(body.conversationId).toBeUndefined();
  });
});
