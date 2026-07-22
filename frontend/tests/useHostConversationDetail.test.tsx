import { act, renderHook, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { useHostConversationDetail } from "../src/hooks/useHostConversationDetail";
import { ConversationMessageType, ConversationSenderType, ConversationStatus, GuestChannel } from "../src/models/enums";

function ok<T>(data: T) {
  return {
    ok: true,
    status: 200,
    json: async () => ({
      success: true,
      message: "ok",
      data,
      errors: [],
      correlationId: "cid"
    })
  };
}

function unauthorized() {
  return {
    ok: false,
    status: 401,
    json: async () => ({
      success: false,
      message: "Unauthorized",
      errors: ["Unauthorized"],
      correlationId: "cid"
    })
  };
}

function detail(conversationId = "c-1", status = ConversationStatus.HumanManaged) {
  return {
    id: conversationId,
    conversationId,
    guestId: "g-1",
    reservationId: "r-1",
    propertyId: "p-1",
    status,
    channel: GuestChannel.Web,
    channelIdentity: null,
    subject: "Need a late checkout",
    guest: {
      id: "g-1",
      fullName: "Ada Lovelace",
      firstName: "Ada",
      lastName: "Lovelace",
      email: "ada@example.com",
      preferredLanguage: "en"
    },
    property: {
      id: "p-1",
      name: "Westlands Apartment",
      city: "Nairobi"
    },
    reservation: {
      id: "r-1",
      confirmationNumber: "ABC123",
      checkInDate: "2026-08-10",
      checkOutDate: "2026-08-14",
      status: 0
    },
    assignedUser: {
      id: "u-1",
      fullName: "Front Desk"
    },
    humanTakeoverEnabled: true,
    requiresHostAttention: true,
    escalationReason: null,
    startedAt: "2026-07-22T10:00:00Z",
    lastActivityAt: "2026-07-22T11:00:00Z",
    closedAt: null,
    latestVisibleMessagePreview: "Can I check out late?",
    latestVisibleMessageSenderType: ConversationSenderType.Guest,
    latestVisibleMessageTimestamp: "2026-07-22T11:00:00Z",
    totalVisibleMessageCount: 2,
    messages: []
  };
}

function history(conversationId = "c-1") {
  return {
    conversationId,
    messages: {
      items: [
        {
          id: "m-1",
          conversationId,
          senderType: ConversationSenderType.Guest,
          messageType: ConversationMessageType.Text,
          content: "Hello",
          isInternal: false,
          sentAt: "2026-07-22T10:00:00Z"
        },
        {
          id: "m-2",
          conversationId,
          senderType: ConversationSenderType.Host,
          messageType: ConversationMessageType.Text,
          content: "Hi there",
          isInternal: false,
          sentAt: "2026-07-22T10:02:00Z"
        }
      ],
      pageNumber: 1,
      pageSize: 100,
      totalCount: 2,
      totalPages: 1
    }
  };
}

describe("useHostConversationDetail", () => {
  beforeEach(() => {
    vi.stubEnv("VITE_STAYFLOW_API_URL", "http://test.local");
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it("loads selected conversation detail and history", async () => {
    const fetchMock = vi.fn().mockImplementation((url: string) => {
      if (url.includes("/messages")) {
        return Promise.resolve(ok(history("c-1")));
      }

      return Promise.resolve(ok(detail("c-1")));
    });

    vi.stubGlobal("fetch", fetchMock);

    const onUnauthorized = vi.fn();

    const { result } = renderHook(() =>
      useHostConversationDetail({
        conversationId: "c-1",
        accessToken: "host-token",
        onUnauthorized
      })
    );

    await waitFor(() => expect(result.current.conversation?.conversationId).toBe("c-1"));
    expect(result.current.messages).toHaveLength(2);
    expect(onUnauthorized).not.toHaveBeenCalled();
  });

  it("handles 401 by calling onUnauthorized", async () => {
    const fetchMock = vi.fn().mockResolvedValue(unauthorized());
    vi.stubGlobal("fetch", fetchMock);

    const onUnauthorized = vi.fn();

    renderHook(() =>
      useHostConversationDetail({
        conversationId: "c-1",
        accessToken: "host-token",
        onUnauthorized
      })
    );

    await waitFor(() => expect(onUnauthorized).toHaveBeenCalled());
  });

  it("sends host reply and refreshes detail + inbox", async () => {
    const fetchMock = vi.fn().mockImplementation((url: string) => {
      if (url.includes("/messages/host")) {
        return Promise.resolve(
          ok({
            id: "m-3",
            conversationId: "c-1",
            senderType: ConversationSenderType.Host,
            messageType: ConversationMessageType.Text,
            content: "On it",
            isInternal: false,
            sentAt: "2026-07-22T10:03:00Z"
          })
        );
      }

      if (url.includes("/messages")) {
        return Promise.resolve(ok(history("c-1")));
      }

      return Promise.resolve(ok(detail("c-1")));
    });

    vi.stubGlobal("fetch", fetchMock);

    const onUnauthorized = vi.fn();
    const onConversationChanged = vi.fn();

    const { result } = renderHook(() =>
      useHostConversationDetail({
        conversationId: "c-1",
        accessToken: "host-token",
        onUnauthorized,
        onConversationChanged
      })
    );

    await waitFor(() => expect(result.current.isLoading).toBe(false));

    await act(async () => {
      const sent = await result.current.sendHostMessage("On it");
      expect(sent).toBe(true);
    });

    await waitFor(() => expect(onConversationChanged).toHaveBeenCalled());

    const calledUrls = fetchMock.mock.calls.map((call) => String(call[0]));
    expect(calledUrls.some((url) => url.endsWith("/conversations/c-1/messages/host"))).toBe(true);
    expect(calledUrls.some((url) => url.endsWith("/conversations/c-1"))).toBe(true);
  });

  it("polls on the offline fallback cadence for selected conversation", async () => {
    vi.useFakeTimers();

    const fetchMock = vi.fn().mockImplementation((url: string) => {
      if (url.includes("/messages")) {
        return Promise.resolve(ok(history("c-1")));
      }

      return Promise.resolve(ok(detail("c-1")));
    });

    vi.stubGlobal("fetch", fetchMock);

    const onUnauthorized = vi.fn();

    renderHook(() =>
      useHostConversationDetail({
        conversationId: "c-1",
        accessToken: "host-token",
        onUnauthorized
      })
    );

    await act(async () => {
      await Promise.resolve();
    });

    const beforePoll = fetchMock.mock.calls.length;

    await act(async () => {
      vi.advanceTimersByTime(12000);
      await Promise.resolve();
    });

    expect(fetchMock.mock.calls.length).toBeGreaterThanOrEqual(beforePoll + 2);
  });

  it("stops polling on unmount", async () => {
    vi.useFakeTimers();

    const fetchMock = vi.fn().mockImplementation((url: string) => {
      if (url.includes("/messages")) {
        return Promise.resolve(ok(history("c-1")));
      }

      return Promise.resolve(ok(detail("c-1")));
    });

    vi.stubGlobal("fetch", fetchMock);

    const onUnauthorized = vi.fn();

    const { unmount } = renderHook(() =>
      useHostConversationDetail({
        conversationId: "c-1",
        accessToken: "host-token",
        onUnauthorized
      })
    );

    await act(async () => {
      await Promise.resolve();
    });

    unmount();
    const countAfterUnmount = fetchMock.mock.calls.length;

    await act(async () => {
      vi.advanceTimersByTime(30000);
    });

    expect(fetchMock.mock.calls.length).toBe(countAfterUnmount);
  });

  it("loads a new conversation when selection changes", async () => {
    const fetchMock = vi.fn().mockImplementation((url: string) => {
      if (url.includes("/conversations/c-2/messages")) {
        return Promise.resolve(ok(history("c-2")));
      }

      if (url.includes("/conversations/c-2")) {
        return Promise.resolve(ok(detail("c-2")));
      }

      if (url.includes("/messages")) {
        return Promise.resolve(ok(history("c-1")));
      }

      return Promise.resolve(ok(detail("c-1")));
    });

    vi.stubGlobal("fetch", fetchMock);

    const onUnauthorized = vi.fn();

    const { result, rerender } = renderHook(
      ({ conversationId }) =>
        useHostConversationDetail({
          conversationId,
          accessToken: "host-token",
          onUnauthorized
        }),
      {
        initialProps: {
          conversationId: "c-1"
        }
      }
    );

    await waitFor(() => expect(result.current.conversation?.conversationId).toBe("c-1"));

    rerender({ conversationId: "c-2" });

    await waitFor(() => {
      const urls = fetchMock.mock.calls.map((call) => String(call[0]));
      expect(urls.some((url) => url.includes("/conversations/c-2"))).toBe(true);
      expect(urls.some((url) => url.includes("/conversations/c-2/messages"))).toBe(true);
    });
  });
});
