import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import App from "../src/App";
import { ConversationSenderType, ConversationStatus, GuestChannel } from "../src/models/enums";
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

function loginResponse() {
  return apiSuccess({
    accessToken: "host-token",
    refreshToken: "refresh",
    expiresAt: "2026-07-22T12:00:00Z"
  });
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
      guestId: "g-1",
      fullName: "",
      preferredLanguage: "en",
      firstName: "Ada",
      lastName: "Lovelace",
      email: "ada@example.com"
    },
    property: {
      id: "p-1",
      propertyId: "p-1",
      name: "Westlands Apartment",
      city: "Nairobi"
    },
    reservation: {
      id: "r-1",
      reservationId: "r-1",
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

function listResponse(items = [conversationRow()], page = 1, totalPages = 1) {
  return apiSuccess({
    items,
    totalCount: items.length,
    page,
    pageSize: 10,
    totalPages
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

  it("successful login loads inbox rows", async () => {
    window.history.pushState({}, "", "/host/conversations");
    vi.stubGlobal("fetch", vi.fn().mockResolvedValueOnce(loginResponse()).mockResolvedValueOnce(listResponse()));

    render(<App />);

    await signIn();

    await waitFor(() => expect(screen.getByText(/westlands apartment/i)).toBeInTheDocument());
    expect(screen.getByText(/can i check in early/i)).toBeInTheDocument();
  });

  it("failed login shows error", async () => {
    window.history.pushState({}, "", "/host/conversations");
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(apiFailure("Invalid credentials", 401)));

    render(<App />);

    await signIn();

    await waitFor(() => expect(screen.getByText(/session has expired|invalid credentials/i)).toBeInTheDocument());
  });

  it("renders safe guest and property fallbacks", async () => {
    window.history.pushState({}, "", "/host/conversations");

    const fallbackItem = {
      ...conversationRow("c-2"),
      guest: null,
      property: null,
      reservation: null,
      latestVisibleMessagePreview: null
    };

    vi.stubGlobal("fetch", vi.fn().mockResolvedValueOnce(loginResponse()).mockResolvedValueOnce(listResponse([fallbackItem])));

    render(<App />);
    await signIn();

    await waitFor(() => expect(screen.getByRole("heading", { name: "Guest" })).toBeInTheDocument());
    expect(screen.getByText(/property unavailable/i)).toBeInTheDocument();
    expect(screen.getByText(/no visible messages yet/i)).toBeInTheDocument();
  });

  it("status filter changes request", async () => {
    window.history.pushState({}, "", "/host/conversations");
    const fetchMock = vi.fn().mockResolvedValueOnce(loginResponse()).mockResolvedValue(listResponse());
    vi.stubGlobal("fetch", fetchMock);

    render(<App />);
    const user = await signIn();

    await waitFor(() => expect(screen.getByLabelText(/status/i)).toBeInTheDocument());
    await user.selectOptions(screen.getByLabelText(/status/i), String(ConversationStatus.Closed));

    await waitFor(() => {
      const listCall = fetchMock.mock.calls.find((call) => String(call[0]).includes("/conversations?"));
      expect(listCall).toBeDefined();
    });

    const listCalls = fetchMock.mock.calls.filter((call) => String(call[0]).includes("/conversations?"));
    const latestUrl = new URL(String(listCalls[listCalls.length - 1][0]));
    expect(latestUrl.searchParams.get("status")).toBe(String(ConversationStatus.Closed));
  });

  it("host-attention filter changes request", async () => {
    window.history.pushState({}, "", "/host/conversations");
    const fetchMock = vi.fn().mockResolvedValueOnce(loginResponse()).mockResolvedValue(listResponse());
    vi.stubGlobal("fetch", fetchMock);

    render(<App />);
    const user = await signIn();

    await waitFor(() => expect(screen.getByLabelText(/requires host attention/i)).toBeInTheDocument());
    await user.click(screen.getByLabelText(/requires host attention/i));

    await waitFor(() => {
      const listCalls = fetchMock.mock.calls.filter((call) => String(call[0]).includes("/conversations?"));
      const latestUrl = new URL(String(listCalls[listCalls.length - 1][0]));
      expect(latestUrl.searchParams.get("requiresHostAttention")).toBe("true");
    });
  });

  it("debounced search changes request", async () => {
    window.history.pushState({}, "", "/host/conversations");
    const fetchMock = vi.fn().mockResolvedValueOnce(loginResponse()).mockResolvedValue(listResponse());
    vi.stubGlobal("fetch", fetchMock);

    render(<App />);
    const user = await signIn();

    await waitFor(() => expect(screen.getByLabelText(/search/i)).toBeInTheDocument());
    await user.type(screen.getByLabelText(/search/i), "Ada");

    await waitFor(() => {
      const listCalls = fetchMock.mock.calls.filter((call) => String(call[0]).includes("/conversations?"));
      const latestUrl = new URL(String(listCalls[listCalls.length - 1][0]));
      expect(latestUrl.searchParams.get("search")).toBe("Ada");
    }, { timeout: 2000 });
  });

  it("pagination changes request", async () => {
    window.history.pushState({}, "", "/host/conversations");
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce(loginResponse())
      .mockResolvedValueOnce(listResponse([conversationRow()], 1, 2))
      .mockResolvedValueOnce(listResponse([conversationRow("c-2")], 2, 2));

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

  it("page-size change resets page to 1", async () => {
    window.history.pushState({}, "", "/host/conversations");
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce(loginResponse())
      .mockResolvedValueOnce(listResponse([conversationRow()], 1, 3))
      .mockResolvedValueOnce(listResponse([conversationRow("c-2")], 2, 3))
      .mockResolvedValueOnce(listResponse([conversationRow("c-3")], 1, 2));

    vi.stubGlobal("fetch", fetchMock);

    render(<App />);
    const user = await signIn();

    await waitFor(() => expect(screen.getByRole("button", { name: /next/i })).toBeInTheDocument());
    await user.click(screen.getByRole("button", { name: /next/i }));

    await waitFor(() => expect(screen.getByText(/page 2 of/i)).toBeInTheDocument());
    await user.selectOptions(screen.getByLabelText(/page size/i), "25");

    await waitFor(() => {
      const listCalls = fetchMock.mock.calls.filter((call) => String(call[0]).includes("/conversations?"));
      const latestUrl = new URL(String(listCalls[listCalls.length - 1][0]));
      expect(latestUrl.searchParams.get("page")).toBe("1");
      expect(latestUrl.searchParams.get("pageSize")).toBe("25");
    });
  });

  it("shows loading and then empty state", async () => {
    window.history.pushState({}, "", "/host/conversations");

    let resolveList: ((value: ReturnType<typeof listResponse>) => void) | undefined;
    const delayedList = new Promise((resolve) => {
      resolveList = resolve;
    });

    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce(loginResponse())
      .mockReturnValueOnce(delayedList)
      .mockResolvedValueOnce(listResponse([]));

    vi.stubGlobal("fetch", fetchMock);

    render(<App />);
    const user = await signIn();

    await waitFor(() => expect(screen.getByText(/loading conversations/i)).toBeInTheDocument());

    if (resolveList) {
      resolveList(listResponse([]));
    }

    await waitFor(() => expect(screen.getByText(/no conversations found/i)).toBeInTheDocument());

    await user.click(screen.getByRole("button", { name: /refresh/i }));
    await waitFor(() => expect(fetchMock).toHaveBeenCalled());
  });

  it("shows api error and allows retry", async () => {
    window.history.pushState({}, "", "/host/conversations");
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce(loginResponse())
      .mockResolvedValueOnce(apiFailure("Server error", 500))
      .mockResolvedValueOnce(listResponse());

    vi.stubGlobal("fetch", fetchMock);

    render(<App />);
    const user = await signIn();

    await waitFor(() => expect(screen.getByText(/guest services are temporarily unavailable/i)).toBeInTheDocument());
    await user.click(screen.getByRole("button", { name: /retry/i }));

    await waitFor(() => expect(screen.getByText(/westlands apartment/i)).toBeInTheDocument());
  });

  it("selecting a row shows detail placeholder with id", async () => {
    window.history.pushState({}, "", "/host/conversations");
    vi.stubGlobal("fetch", vi.fn().mockResolvedValueOnce(loginResponse()).mockResolvedValueOnce(listResponse()));

    render(<App />);
    const user = await signIn();

    await waitFor(() => expect(screen.getByRole("button", { name: /ada lovelace/i })).toBeInTheDocument());
    await user.click(screen.getByRole("button", { name: /ada lovelace/i }));

    expect(screen.getByText(/sprint 4 part 2b/i)).toBeInTheDocument();
    expect(screen.getByText(/selected conversation id/i)).toBeInTheDocument();
    expect(screen.getByText("c-1")).toBeInTheDocument();
  });

  it("logout returns to login panel", async () => {
    window.history.pushState({}, "", "/host/conversations");
    vi.stubGlobal("fetch", vi.fn().mockResolvedValueOnce(loginResponse()).mockResolvedValueOnce(listResponse()));

    render(<App />);
    const user = await signIn();

    await waitFor(() => expect(screen.getByRole("button", { name: /sign out/i })).toBeInTheDocument());
    await user.click(screen.getByRole("button", { name: /sign out/i }));

    expect(screen.getByRole("heading", { name: /host sign in/i })).toBeInTheDocument();
  });

  it("renders guest demo page for non-host path", () => {
    window.history.pushState({}, "", "/");
    vi.stubGlobal("fetch", vi.fn());

    render(<App />);

    expect(screen.getByText(/guest concierge chat widget/i)).toBeInTheDocument();
  });
});
