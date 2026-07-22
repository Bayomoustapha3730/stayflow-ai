import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import App from "../src/App";
import {
  ConversationMessageType,
  ConversationSenderType,
  ConversationStatus,
  GuestChannel
} from "../src/models/enums";
import type { ConversationSummary } from "../src/models/hostConversations";

function apiSuccess<T>(data: T) {
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

function apiFailure(message = "Request failed", status = 500) {
  return {
    ok: false,
    status,
    json: async () => ({
      success: false,
      message,
      errors: [message],
      correlationId: "cid"
    })
  };
}

function conversationRow(id = "c-1"): ConversationSummary {
  return {
    id,
    conversationId: id,
    guestId: "g-1",
    reservationId: "r-1",
    propertyId: "p-1",
    status: ConversationStatus.AwaitingHost,
    channel: GuestChannel.Web,
    channelIdentity: null,
    subject: "Check-in question",
    guest: {
      id: "g-1",
      fullName: "",
      preferredLanguage: "en",
      firstName: "Ada",
      lastName: "Lovelace",
      email: "ada@example.com"
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
    assignedUser: null,
    humanTakeoverEnabled: true,
    requiresHostAttention: true,
    escalationReason: null,
    startedAt: "2026-07-20T10:00:00Z",
    lastActivityAt: "2026-07-22T10:00:00Z",
    closedAt: null,
    latestVisibleMessagePreview: "Can I check in early?",
    latestVisibleMessageSenderType: ConversationSenderType.Guest,
    latestVisibleMessageTimestamp: "2026-07-22T10:00:00Z",
    totalVisibleMessageCount: 4
  };
}

function conversationDetail(conversationId = "c-1", status = ConversationStatus.HumanManaged, humanTakeoverEnabled = true) {
  return {
    ...conversationRow(conversationId),
    status,
    humanTakeoverEnabled,
    messages: []
  };
}

function messageHistory(conversationId = "c-1") {
  return {
    conversationId,
    messages: {
      items: [
        {
          id: "m-1",
          conversationId,
          senderType: ConversationSenderType.Guest,
          messageType: ConversationMessageType.Text,
          content: "Can I check in early?",
          isInternal: false,
          sentAt: "2026-07-22T10:00:00Z"
        }
      ],
      pageNumber: 1,
      pageSize: 100,
      totalCount: 1,
      totalPages: 1
    }
  };
}

function listResponse(items = [conversationRow()], page = 1, totalPages = 1) {
  return {
    items,
    totalCount: items.length,
    page,
    pageSize: 10,
    totalPages
  };
}

function createHostFetchMock(items = [conversationRow()]) {
  return vi.fn().mockImplementation((url: string, options?: RequestInit) => {
    if (url.endsWith("/auth/login")) {
      return Promise.resolve(
        apiSuccess({
          accessToken: "host-token",
          refreshToken: "refresh",
          expiresAt: "2026-07-22T12:00:00Z"
        })
      );
    }

    if (url.includes("/conversations?") && options?.method === "GET") {
      const parsed = new URL(url);
      const page = Number(parsed.searchParams.get("page") ?? "1");
      const totalPages = page > 1 ? page : 2;
      const pageItem = page > 1 ? [conversationRow("c-2")] : items;
      return Promise.resolve(apiSuccess(listResponse(pageItem, page, totalPages)));
    }

    if (url.includes("/messages/host")) {
      return Promise.resolve(
        apiSuccess({
          id: "m-2",
          conversationId: "c-1",
          senderType: ConversationSenderType.Host,
          messageType: ConversationMessageType.Text,
          content: "Host reply",
          isInternal: false,
          sentAt: "2026-07-22T10:02:00Z"
        })
      );
    }

    if (url.includes("/notes")) {
      return Promise.resolve(
        apiSuccess({
          id: "m-3",
          conversationId: "c-1",
          senderType: ConversationSenderType.System,
          messageType: ConversationMessageType.InternalNote,
          content: "Internal note",
          isInternal: true,
          sentAt: "2026-07-22T10:03:00Z"
        })
      );
    }

    if (url.includes("/resolve") || url.includes("/close") || url.includes("/human-takeover") || url.includes("/return-to-ai")) {
      return Promise.resolve(apiSuccess(conversationDetail("c-1")));
    }

    if (url.includes("/conversations/c-1/messages")) {
      return Promise.resolve(apiSuccess(messageHistory("c-1")));
    }

    if (url.includes("/conversations/c-2/messages")) {
      return Promise.resolve(apiSuccess(messageHistory("c-2")));
    }

    if (url.endsWith("/conversations/c-1")) {
      return Promise.resolve(apiSuccess(conversationDetail("c-1")));
    }

    if (url.endsWith("/conversations/c-2")) {
      return Promise.resolve(apiSuccess(conversationDetail("c-2")));
    }

    return Promise.resolve(apiFailure("Unhandled route", 500));
  });
}

async function signIn(user = userEvent.setup()) {
  await user.clear(screen.getByLabelText(/email/i));
  await user.type(screen.getByLabelText(/email/i), "host@example.com");
  await user.clear(screen.getByLabelText(/password/i));
  await user.type(screen.getByLabelText(/password/i), "Password123!");
  await user.click(screen.getByRole("button", { name: /sign in/i }));
  return user;
}

describe("HostInboxPage via App route", () => {
  beforeEach(() => {
    sessionStorage.clear();
    vi.stubEnv("VITE_STAYFLOW_API_URL", "http://test.local");
    vi.stubEnv("VITE_DEMO_EMAIL", "host@example.com");
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it("shows login panel when unauthenticated on host route", () => {
    window.history.pushState({}, "", "/host/conversations");
    vi.stubGlobal("fetch", vi.fn());

    render(<App />);

    expect(screen.getByRole("heading", { name: /host sign in/i })).toBeInTheDocument();
  });

  it("successful login loads inbox and conversation detail workspace", async () => {
    window.history.pushState({}, "", "/host/conversations");
    vi.stubGlobal("fetch", createHostFetchMock());

    render(<App />);

    await signIn();

    await waitFor(() => expect(screen.getByText(/westlands apartment/i)).toBeInTheDocument());
    await waitFor(() => expect(screen.getByRole("heading", { name: /timeline/i })).toBeInTheDocument(), {
      timeout: 3000
    });
  });

  it("status filter changes request", async () => {
    window.history.pushState({}, "", "/host/conversations");
    const fetchMock = createHostFetchMock();
    vi.stubGlobal("fetch", fetchMock);

    render(<App />);
    const user = await signIn();

    await waitFor(() => expect(screen.getByLabelText(/status/i)).toBeInTheDocument());
    await user.selectOptions(screen.getByLabelText(/status/i), String(ConversationStatus.Closed));

    await waitFor(() => {
      const listCalls = fetchMock.mock.calls.filter((call) => String(call[0]).includes("/conversations?"));
      const latestUrl = new URL(String(listCalls[listCalls.length - 1][0]));
      expect(latestUrl.searchParams.get("status")).toBe(String(ConversationStatus.Closed));
    });
  });

  it("host-attention filter changes request", async () => {
    window.history.pushState({}, "", "/host/conversations");
    const fetchMock = createHostFetchMock();
    vi.stubGlobal("fetch", fetchMock);

    render(<App />);
    const user = await signIn();

    await user.click(screen.getByLabelText(/requires host attention/i));

    await waitFor(() => {
      const listCalls = fetchMock.mock.calls.filter((call) => String(call[0]).includes("/conversations?"));
      const latestUrl = new URL(String(listCalls[listCalls.length - 1][0]));
      expect(latestUrl.searchParams.get("requiresHostAttention")).toBe("true");
    });
  });

  it("debounced search changes request", async () => {
    window.history.pushState({}, "", "/host/conversations");
    const fetchMock = createHostFetchMock();
    vi.stubGlobal("fetch", fetchMock);

    render(<App />);
    const user = await signIn();

    await user.type(screen.getByLabelText(/search/i), "Ada");

    await waitFor(() => {
      const listCalls = fetchMock.mock.calls.filter((call) => String(call[0]).includes("/conversations?"));
      const latestUrl = new URL(String(listCalls[listCalls.length - 1][0]));
      expect(latestUrl.searchParams.get("search")).toBe("Ada");
    }, { timeout: 2000 });
  });

  it("pagination next updates list query", async () => {
    window.history.pushState({}, "", "/host/conversations");
    const fetchMock = createHostFetchMock();
    vi.stubGlobal("fetch", fetchMock);

    render(<App />);
    const user = await signIn();

    await waitFor(() => expect(screen.getByRole("button", { name: /next/i })).toBeInTheDocument());
    await user.click(screen.getByRole("button", { name: /next/i }));

    await waitFor(() => {
      const listCalls = fetchMock.mock.calls.filter((call) => String(call[0]).includes("/conversations?"));
      const latestUrl = new URL(String(listCalls[listCalls.length - 1][0]));
      expect(latestUrl.searchParams.get("page")).toBe("2");
    });
  });

  it("inbox refreshes after resolve action", async () => {
    window.history.pushState({}, "", "/host/conversations");
    const fetchMock = createHostFetchMock();
    vi.stubGlobal("fetch", fetchMock);

    render(<App />);
    const user = await signIn();

    await waitFor(() => expect(screen.getByRole("button", { name: /resolve conversation/i })).toBeInTheDocument());

    const listCallsBefore = fetchMock.mock.calls.filter((call) => String(call[0]).includes("/conversations?")).length;

    await user.click(screen.getByRole("button", { name: /resolve conversation/i }));
    await user.click(screen.getByRole("button", { name: /confirm resolve/i }));

    await waitFor(() => {
      const listCallsAfter = fetchMock.mock.calls.filter((call) => String(call[0]).includes("/conversations?")).length;
      expect(listCallsAfter).toBeGreaterThan(listCallsBefore);
    });
  });

  it("guest demo route still renders for non-host path", () => {
    window.history.pushState({}, "", "/");
    vi.stubGlobal("fetch", vi.fn());

    render(<App />);

    expect(screen.getByText(/guest concierge chat widget/i)).toBeInTheDocument();
  });
});
