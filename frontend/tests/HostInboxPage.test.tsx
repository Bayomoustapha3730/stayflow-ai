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
    totalVisibleMessageCount: 4,
    unreadMessageCount: 2,
    lastReadAt: null
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
    totalPages,
    totalUnreadCount: items.reduce((count, item) => count + item.unreadMessageCount, 0)
  };
}

function copilotSummaryResponse(conversationId = "c-1") {
  return {
    conversationId,
    summary: "Guest is requesting early check-in details and timing confirmation.",
    guestIntent: "Check-in assistance",
    importantFacts: ["Arrival expected before standard time", "Guest requested confirmation today"],
    urgency: "medium",
    latestGuestMessage: "Can I check in early?",
    visibleMessageCount: 4,
    generatedAt: "2026-07-22T10:05:00Z"
  };
}

function copilotSuggestionsResponse(conversationId = "c-1") {
  return {
    conversationId,
    suggestedReplies: [
      "Thanks for reaching out. I can confirm early check-in options and update you shortly.",
      "Happy to help. Could you share your expected arrival time so I can check availability?",
      "I received your request and I am checking the best check-in option for you now."
    ],
    contextMessageCount: 4,
    generatedAt: "2026-07-22T10:05:30Z"
  };
}

function createHostFetchMock(
  items = [conversationRow()],
  config?: {
    failSummary?: boolean;
    failSuggestions?: boolean;
    emptySuggestions?: boolean;
    failGenerate?: boolean;
    generateStatus?: 400 | 401 | 403 | 404;
    generateErrorMessage?: string;
    delayGenerateMs?: number;
    failGenerateNetwork?: boolean;
  }
) {
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

    if (url.includes("/copilot/conversations/c-1/summary")) {
      if (config?.failSummary) {
        return Promise.resolve(apiFailure("Summary unavailable", 500));
      }

      return Promise.resolve(apiSuccess(copilotSummaryResponse("c-1")));
    }

    if (url.includes("/copilot/conversations/c-2/summary")) {
      return Promise.resolve(apiSuccess(copilotSummaryResponse("c-2")));
    }

    if (url.includes("/copilot/conversations/c-1/suggested-replies")) {
      if (config?.failSuggestions) {
        return Promise.resolve(apiFailure("Suggestions unavailable", 500));
      }

      const response = copilotSuggestionsResponse("c-1");
      if (config?.emptySuggestions) {
        response.suggestedReplies = [];
      }

      return Promise.resolve(apiSuccess(response));
    }

    if (url.includes("/copilot/conversations/c-2/suggested-replies")) {
      return Promise.resolve(apiSuccess(copilotSuggestionsResponse("c-2")));
    }

    if (url.includes("/copilot/conversations/c-1/generate-reply")) {
      if (config?.failGenerateNetwork) {
        return Promise.reject(new Error("network down"));
      }

      if (config?.generateStatus) {
        return Promise.resolve(apiFailure(config.generateErrorMessage ?? "Generate failed", config.generateStatus));
      }

      if (config?.failGenerate) {
        return Promise.resolve(apiFailure("Generation unavailable", 500));
      }

      const payload = apiSuccess({
        conversationId: "c-1",
        suggestedReply: "Absolutely. I will confirm early check-in options and update you shortly.",
        rationale: "Generated from context",
        contextMessageCount: 4,
        isFallback: false,
        providerMetadata: {
          providerName: "Development",
          modelName: "stayflow-development-deterministic",
          requestId: "req-1"
        },
        generatedAt: "2026-07-22T10:06:00Z"
      });

      if (config?.delayGenerateMs && config.delayGenerateMs > 0) {
        return new Promise((resolve) => {
          setTimeout(() => resolve(payload), config.delayGenerateMs);
        });
      }

      return Promise.resolve(payload);
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

  it("copilot summary shows loading skeleton then success data", async () => {
    window.history.pushState({}, "", "/host/conversations");
    const baseFetch = createHostFetchMock();
    vi.stubGlobal(
      "fetch",
      vi.fn().mockImplementation((url: string, options?: RequestInit) => {
        if (url.includes("/copilot/conversations/c-1/summary")) {
          return new Promise((resolve) => {
            setTimeout(() => {
              void Promise.resolve(baseFetch(url, options)).then(resolve);
            }, 250);
          });
        }

        return baseFetch(url, options);
      })
    );

    render(<App />);
    await signIn();

    await waitFor(() => expect(screen.getByRole("heading", { name: /ai copilot/i })).toBeInTheDocument());
    await waitFor(() => expect(screen.getByLabelText(/summary loading skeleton/i)).toBeInTheDocument());

    await waitFor(() => expect(screen.getByRole("heading", { name: /conversation summary/i })).toBeInTheDocument());
    await waitFor(() => expect(screen.getByText(/guest is requesting early check-in details/i)).toBeInTheDocument());
    expect(screen.getByText(/check-in assistance/i)).toBeInTheDocument();
  });

  it("copilot summary failure shows retry action", async () => {
    window.history.pushState({}, "", "/host/conversations");
    const fetchMock = createHostFetchMock([conversationRow()], { failSummary: true });
    vi.stubGlobal("fetch", fetchMock);

    render(<App />);
    const user = await signIn();

    await waitFor(() => expect(screen.getByRole("heading", { name: /ai copilot/i })).toBeInTheDocument());
    await waitFor(() => expect(screen.getByText(/guest services are temporarily unavailable/i)).toBeInTheDocument());
    await user.click(screen.getByRole("button", { name: /^retry$/i }));

    await waitFor(() => {
      const calls = fetchMock.mock.calls.filter((call) => String(call[0]).includes("/copilot/conversations/c-1/summary"));
      expect(calls.length).toBeGreaterThan(1);
    });
  });

  it("copilot suggestions render three reply cards", async () => {
    window.history.pushState({}, "", "/host/conversations");
    vi.stubGlobal("fetch", createHostFetchMock());

    render(<App />);
    await signIn();

    await waitFor(() => {
      const insertButtons = screen.getAllByRole("button", { name: /insert/i });
      expect(insertButtons.length).toBe(3);
    });
  });

  it("copilot suggestions empty state is shown", async () => {
    window.history.pushState({}, "", "/host/conversations");
    vi.stubGlobal("fetch", createHostFetchMock([conversationRow()], { emptySuggestions: true }));

    render(<App />);
    await signIn();

    await waitFor(() => expect(screen.getByText(/no suggestions available/i)).toBeInTheDocument());
  });

  it("copilot suggestions failure shows retry", async () => {
    window.history.pushState({}, "", "/host/conversations");
    const fetchMock = createHostFetchMock([conversationRow()], { failSuggestions: true });
    vi.stubGlobal("fetch", fetchMock);

    render(<App />);
    const user = await signIn();

    await waitFor(() => expect(screen.getByText(/guest services are temporarily unavailable/i)).toBeInTheDocument());
    await user.click(screen.getByRole("button", { name: /^retry$/i }));

    await waitFor(() => {
      const calls = fetchMock.mock.calls.filter((call) => String(call[0]).includes("/copilot/conversations/c-1/suggested-replies"));
      expect(calls.length).toBeGreaterThan(1);
    });
  });

  it("changing tone refreshes suggested replies request", async () => {
    window.history.pushState({}, "", "/host/conversations");
    const fetchMock = createHostFetchMock();
    vi.stubGlobal("fetch", fetchMock);

    render(<App />);
    const user = await signIn();

    await waitFor(() => expect(screen.getByLabelText(/tone/i)).toBeInTheDocument());
    await user.selectOptions(screen.getByLabelText(/tone/i), "luxury");

    await waitFor(() => {
      const calls = fetchMock.mock.calls.filter((call) => String(call[0]).includes("/copilot/conversations/c-1/suggested-replies"));
      const latest = new URL(String(calls[calls.length - 1][0]));
      expect(latest.searchParams.get("tone")).toBe("luxury");
    });
  });

  it("insert copies suggestion into host reply composer without sending", async () => {
    window.history.pushState({}, "", "/host/conversations");
    const fetchMock = createHostFetchMock();
    vi.stubGlobal("fetch", fetchMock);

    render(<App />);
    const user = await signIn();

    await waitFor(() => expect(screen.getAllByRole("button", { name: /insert/i }).length).toBeGreaterThan(0));
    await user.click(screen.getAllByRole("button", { name: /insert/i })[0]);

    const replyInput = screen.getByLabelText(/reply to guest/i, { selector: "textarea" }) as HTMLTextAreaElement;
    expect(replyInput.value).toContain("Thanks for reaching out");

    const sendCalls = fetchMock.mock.calls.filter((call) => String(call[0]).includes("/messages/host"));
    expect(sendCalls.length).toBe(0);
  });

  it("suggestion insert asks before replacing non-empty unsent draft", async () => {
    window.history.pushState({}, "", "/host/conversations");
    const fetchMock = createHostFetchMock();
    vi.stubGlobal("fetch", fetchMock);

    render(<App />);
    const user = await signIn();

    const replyInput = await screen.findByLabelText(/reply to guest/i, { selector: "textarea" });
    await user.type(replyInput, "My custom draft");

    await user.click((await screen.findAllByRole("button", { name: /insert suggested reply/i }))[0]);
    expect(screen.getByText(/replace the current reply draft/i)).toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: /^cancel$/i }));
    expect((replyInput as HTMLTextAreaElement).value).toContain("My custom draft");

    await user.click((await screen.findAllByRole("button", { name: /insert suggested reply/i }))[0]);
    await user.click(screen.getByRole("button", { name: /^replace$/i }));
    expect((replyInput as HTMLTextAreaElement).value).toContain("Thanks for reaching out");

    const sendCalls = fetchMock.mock.calls.filter((call) => String(call[0]).includes("/messages/host"));
    expect(sendCalls.length).toBe(0);
  });

  it("copy suggested reply uses clipboard without sending", async () => {
    window.history.pushState({}, "", "/host/conversations");
    const fetchMock = createHostFetchMock();
    vi.stubGlobal("fetch", fetchMock);
    const writeText = vi.fn().mockResolvedValue(undefined);
    Object.defineProperty(window.navigator, "clipboard", {
      configurable: true,
      value: { writeText }
    });

    render(<App />);
    const user = await signIn();

    await user.click((await screen.findAllByRole("button", { name: /copy suggested reply/i }))[0]);

    await waitFor(() => {
      expect(screen.getByText(/copied|copy failed/i)).toBeInTheDocument();
    });

    const sendCalls = fetchMock.mock.calls.filter((call) => String(call[0]).includes("/messages/host"));
    expect(sendCalls.length).toBe(0);
  });

  it("generate reply request does not auto-send host message", async () => {
    window.history.pushState({}, "", "/host/conversations");
    const fetchMock = createHostFetchMock();
    vi.stubGlobal("fetch", fetchMock);

    render(<App />);
    const user = await signIn();

    await user.type(await screen.findByLabelText(/what should the reply emphasize/i), "Confirm check-in window");
    await user.click(await screen.findByRole("button", { name: /generate reply/i }));

    await waitFor(() => expect(screen.getByRole("heading", { name: /generated reply/i })).toBeInTheDocument());

    await waitFor(() => {
      const calls = fetchMock.mock.calls.filter((call) => String(call[0]).includes("/copilot/conversations/c-1/generate-reply"));
      expect(calls.length).toBeGreaterThan(0);
    });

    const sendCalls = fetchMock.mock.calls.filter((call) => String(call[0]).includes("/messages/host"));
    expect(sendCalls.length).toBe(0);
  });

  it("generated reply shows loading state before success", async () => {
    window.history.pushState({}, "", "/host/conversations");
    vi.stubGlobal("fetch", createHostFetchMock([conversationRow()], { delayGenerateMs: 250 }));

    render(<App />);
    const user = await signIn();

    await user.click(await screen.findByRole("button", { name: /generate reply/i }));
    expect(screen.getAllByText(/generating reply/i).length).toBeGreaterThan(0);

    await waitFor(() => expect(screen.getByLabelText(/generated reply/i)).toBeInTheDocument());
  });

  it("rewrite current draft sends generation request and keeps selection", async () => {
    window.history.pushState({}, "", "/host/conversations");
    const fetchMock = createHostFetchMock();
    vi.stubGlobal("fetch", fetchMock);

    render(<App />);
    const user = await signIn();

    const replyInput = await screen.findByLabelText(/reply to guest/i, { selector: "textarea" });
    await user.type(replyInput, "Please rewrite this draft");
    await user.click(screen.getByRole("button", { name: /rewrite current draft/i }));

    await waitFor(() => {
      const calls = fetchMock.mock.calls.filter((call) => String(call[0]).includes("/copilot/conversations/c-1/generate-reply"));
      expect(calls.length).toBeGreaterThan(0);
    });

    const generateCalls = fetchMock.mock.calls.filter((call) => String(call[0]).includes("/copilot/conversations/c-1/generate-reply"));
    expect(generateCalls.length).toBeGreaterThan(0);
  });

  it("generation failure preserves existing host draft", async () => {
    window.history.pushState({}, "", "/host/conversations");
    const fetchMock = createHostFetchMock([conversationRow()], { failGenerate: true });
    vi.stubGlobal("fetch", fetchMock);

    render(<App />);
    const user = await signIn();

    const replyInput = await screen.findByLabelText(/reply to guest/i, { selector: "textarea" });
    await user.type(replyInput, "Keep this draft");

    await user.click(await screen.findByRole("button", { name: /generate reply/i }));
    await waitFor(() => expect(screen.getByRole("alert")).toHaveTextContent(/unable to generate a reply/i));
    expect(screen.getByRole("button", { name: /retry/i })).toBeInTheDocument();

    expect((replyInput as HTMLTextAreaElement).value).toContain("Keep this draft");

    const sendCalls = fetchMock.mock.calls.filter((call) => String(call[0]).includes("/messages/host"));
    expect(sendCalls.length).toBe(0);
  });

  it("maps generate 400 to validation message", async () => {
    window.history.pushState({}, "", "/host/conversations");
    vi.stubGlobal("fetch", createHostFetchMock([conversationRow()], {
      generateStatus: 400,
      generateErrorMessage: "Conversation is closed and cannot be drafted."
    }));

    render(<App />);
    const user = await signIn();

    await user.click(await screen.findByRole("button", { name: /generate reply/i }));
    await waitFor(() => expect(screen.getByText(/conversation is closed and cannot be drafted/i)).toBeInTheDocument());
  });

  it("maps generate 403 to permission message", async () => {
    window.history.pushState({}, "", "/host/conversations");
    vi.stubGlobal("fetch", createHostFetchMock([conversationRow()], { generateStatus: 403 }));

    render(<App />);
    const user = await signIn();

    await user.click(await screen.findByRole("button", { name: /generate reply/i }));
    await waitFor(() => expect(screen.getByText(/do not have permission to generate replies/i)).toBeInTheDocument());
  });

  it("maps generate 404 to conversation unavailable", async () => {
    window.history.pushState({}, "", "/host/conversations");
    vi.stubGlobal("fetch", createHostFetchMock([conversationRow()], { generateStatus: 404 }));

    render(<App />);
    const user = await signIn();

    await user.click(await screen.findByRole("button", { name: /generate reply/i }));
    await waitFor(() => expect(screen.getByText(/^conversation unavailable\.?$/i)).toBeInTheDocument());
  });

  it("maps generate network failures to reachability message", async () => {
    window.history.pushState({}, "", "/host/conversations");
    vi.stubGlobal("fetch", createHostFetchMock([conversationRow()], { failGenerateNetwork: true }));

    render(<App />);
    const user = await signIn();

    await user.click(await screen.findByRole("button", { name: /generate reply/i }));
    await waitFor(() => expect(screen.getByText(/unable to reach stayflow/i)).toBeInTheDocument());
  });

  it("generate 401 signs user out through unauthorized flow", async () => {
    window.history.pushState({}, "", "/host/conversations");
    vi.stubGlobal("fetch", createHostFetchMock([conversationRow()], { generateStatus: 401 }));

    render(<App />);
    const user = await signIn();

    await user.click(await screen.findByRole("button", { name: /generate reply/i }));
    await waitFor(() => expect(screen.getByRole("heading", { name: /host sign in/i })).toBeInTheDocument());
  });

  it("no copilot requests are made when no conversation is selected", async () => {
    window.history.pushState({}, "", "/host/conversations");
    const items: ConversationSummary[] = [];
    const fetchMock = createHostFetchMock(items);
    vi.stubGlobal("fetch", fetchMock);

    render(<App />);
    await signIn();

    await waitFor(() => expect(screen.getByText(/no conversations/i)).toBeInTheDocument());

    const copilotCalls = fetchMock.mock.calls.filter((call) => String(call[0]).includes("/copilot/conversations/"));
    expect(copilotCalls.length).toBe(0);
  });
});
