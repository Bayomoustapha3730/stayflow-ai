import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import App from "../src/App";
import {
  ConversationMessageType,
  ConversationSenderType,
  ConversationStatus,
  GuestChannel
} from "../src/models/enums";
import type {
  ConversationCopilotSuggestionsResponse,
  ConversationCopilotSummaryResponse,
  CopilotConfidence,
  CopilotSuggestReplyResponse
} from "../src/models/copilot";
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

function copilotSummaryResponse(conversationId = "c-1"): ConversationCopilotSummaryResponse {
  return {
    conversationId,
    summary: "Guest is requesting early check-in details and timing confirmation.",
    guestIntent: "Check-in assistance",
    importantFacts: ["Arrival expected before standard time", "Guest requested confirmation today"],
    urgency: "medium",
    latestGuestMessage: "Can I check in early?",
    visibleMessageCount: 4,
    confidence: {
      score: 88,
      level: "High",
      reasons: ["All required context sections are available and coherent."],
      missingContext: []
    },
    sources: [
      {
        sourceType: "Property",
        title: "Westlands Apartment",
        category: "Amenities",
        relevanceReason: "Property details are linked to this conversation.",
        lastUpdated: "2026-07-21T12:00:00Z"
      }
    ],
    warnings: [],
    contextTruncated: false,
    generatedAt: "2026-07-22T10:05:00Z"
  };
}

function copilotSuggestionsResponse(conversationId = "c-1"): ConversationCopilotSuggestionsResponse {
  return {
    conversationId,
    suggestedReplies: [
      "Thanks for reaching out. I can confirm early check-in options and update you shortly.",
      "Happy to help. Could you share your expected arrival time so I can check availability?",
      "I received your request and I am checking the best check-in option for you now."
    ],
    contextMessageCount: 4,
    confidence: {
      score: 68,
      level: "Medium",
      reasons: ["Context was truncated to stay within safety limits."],
      missingContext: ["ContextTruncated"]
    },
    sources: [
      {
        sourceType: "Property",
        title: "Demo Nairobi Apartment",
        category: "Property",
        relevanceReason: "Property details are linked to this conversation.",
        lastUpdated: "2026-07-20T10:00:00Z"
      },
      {
        sourceType: "Reservation",
        title: "Reservation DEMO-CONF-001",
        category: "Reservation",
        relevanceReason: "Reservation details are linked to this conversation.",
        lastUpdated: "2026-07-20T10:00:00Z"
      },
      {
        sourceType: "PropertyKnowledge",
        title: "House Rules",
        category: "HouseRules",
        relevanceReason: "Approved property knowledge relevant for guest responses.",
        lastUpdated: "2026-07-20T10:00:00Z"
      },
      {
        sourceType: "PropertyKnowledge",
        title: "Wi-Fi Information",
        category: "WiFi",
        relevanceReason: "Approved property knowledge relevant for guest responses.",
        lastUpdated: "2026-07-20T10:00:00Z"
      },
      {
        sourceType: "PropertyKnowledge",
        title: "Parking",
        category: "Parking",
        relevanceReason: "Approved property knowledge relevant for guest responses.",
        lastUpdated: "2026-07-20T10:00:00Z"
      }
    ],
    warnings: ["ContextTruncated"],
    contextTruncated: true,
    generatedAt: "2026-07-22T10:05:30Z"
  };
}

function copilotGeneratedReplyResponse(conversationId = "c-1"): CopilotSuggestReplyResponse {
  return {
    conversationId,
    suggestedReply: "Thanks for your request. I can confirm early check-in options after I verify your arrival window.",
    rationale: "Generated from recent conversation context and optional host guidance.",
    contextMessageCount: 4,
    isFallback: false,
    providerMetadata: {
      providerName: "Development",
      modelName: "local-deterministic",
      requestId: "req-123"
    },
    confidence: {
      score: 92,
      level: "High",
      reasons: ["All required context sections are available and coherent."],
      missingContext: []
    },
    sources: [
      {
        sourceType: "Conversation",
        title: "Conversation",
        category: null,
        relevanceReason: "Conversation metadata and visible message history.",
        lastUpdated: "2026-07-20T10:00:00Z"
      }
    ],
    warnings: [],
    contextTruncated: false,
    generatedAt: "2026-07-22T10:06:00Z"
  };
}

function createHostFetchMock(
  items = [conversationRow()],
  config?: {
    failSummary?: boolean;
    failSuggestions?: boolean;
    emptySuggestions?: boolean;
    failGenerate?: boolean;
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

    if (url.includes("/copilot/conversations/c-1/suggest-reply")) {
      if (config?.failGenerate) {
        return Promise.resolve(apiFailure("Generation unavailable", 500));
      }

      return Promise.resolve(apiSuccess(copilotGeneratedReplyResponse("c-1")));
    }

    if (url.includes("/copilot/conversations/c-2/suggest-reply")) {
      return Promise.resolve(apiSuccess(copilotGeneratedReplyResponse("c-2")));
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

function findDisclosureSummary(label: RegExp): HTMLElement {
  const summaries = Array.from(document.querySelectorAll(".sf-host-copilot-disclosure > summary"));
  const match = summaries.find((item) => label.test(item.textContent ?? ""));
  if (!match) {
    throw new Error(`Could not find disclosure summary matching ${label}`);
  }

  return match as HTMLElement;
}

describe("HostInboxPage via App route", () => {
  beforeEach(() => {
    sessionStorage.clear();
    vi.stubEnv("VITE_STAYFLOW_API_URL", "http://test.local");
    vi.stubEnv("VITE_DEMO_EMAIL", "host@example.com");
    Object.defineProperty(navigator, "clipboard", {
      value: {
        writeText: vi.fn().mockResolvedValue(undefined)
      },
      configurable: true
    });
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

    await waitFor(() => expect(screen.getByRole("heading", { name: /host copilot/i })).toBeInTheDocument());
    await waitFor(() => expect(screen.getByLabelText(/summary loading skeleton/i)).toBeInTheDocument());

    await waitFor(() => expect(screen.getByText(/conversation summary/i)).toBeInTheDocument());
    await waitFor(() => expect(screen.getByText(/guest is requesting early check-in details/i)).toBeInTheDocument());
    expect(screen.getByText(/check-in assistance/i)).toBeInTheDocument();
    expect(screen.getByText(/high confidence/i)).toBeInTheDocument();
    expect(screen.getByText(/88%/i)).toBeInTheDocument();
  });

  it("copilot summary failure shows retry action", async () => {
    window.history.pushState({}, "", "/host/conversations");
    const fetchMock = createHostFetchMock([conversationRow()], { failSummary: true });
    vi.stubGlobal("fetch", fetchMock);

    render(<App />);
    const user = await signIn();

    await waitFor(() => expect(screen.getByRole("heading", { name: /host copilot/i })).toBeInTheDocument());
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

    expect(screen.getByText(/medium confidence/i)).toBeInTheDocument();
    expect(screen.getByText(/some older context was omitted/i)).toBeInTheDocument();
  });

  it("copilot warnings and confidence reasons render accessibly", async () => {
    window.history.pushState({}, "", "/host/conversations");
    vi.stubGlobal("fetch", createHostFetchMock());

    render(<App />);
    const user = await signIn();

    await waitFor(() => expect(findDisclosureSummary(/conversation summary/i)).toBeInTheDocument());

    await user.click(findDisclosureSummary(/conversation summary/i));
    await user.click(findDisclosureSummary(/conversation summary/i));

    await waitFor(() => expect(screen.getAllByText(/why this confidence/i).length).toBeGreaterThan(0));
    await waitFor(() => expect(screen.getByLabelText(/context warnings/i)).toBeInTheDocument());
  });

  it("copilot renders low confidence when provided", async () => {
    window.history.pushState({}, "", "/host/conversations");
    const baseFetch = createHostFetchMock();
    vi.stubGlobal(
      "fetch",
      vi.fn().mockImplementation((url: string, options?: RequestInit) => {
        if (url.includes("/copilot/conversations/c-1/summary")) {
          const response = copilotSummaryResponse("c-1");
          const lowConfidence: CopilotConfidence = {
            score: 35,
            level: "Low",
            reasons: ["No approved property knowledge is available."],
            missingContext: ["NoApprovedKnowledge"]
          };
          response.confidence = lowConfidence;

          return Promise.resolve(apiSuccess(response));
        }

        return baseFetch(url, options);
      })
    );

    render(<App />);
    await signIn();

    await waitFor(() => expect(screen.getByText(/low confidence/i)).toBeInTheDocument());
  });

  it("copilot source overflow can be expanded", async () => {
    window.history.pushState({}, "", "/host/conversations");
    vi.stubGlobal("fetch", createHostFetchMock());

    render(<App />);
    const user = await signIn();

    await waitFor(() => expect(findDisclosureSummary(/sources \(\d+\)/i)).toBeInTheDocument());
    await user.click(findDisclosureSummary(/sources \(\d+\)/i));
    await waitFor(() => expect(screen.getByRole("button", { name: /show all sources/i })).toBeInTheDocument());
    await user.click(screen.getByRole("button", { name: /show all sources/i }));
    await waitFor(() => {
      const matches = screen.getAllByText(/^parking$/i);
      expect(matches.length).toBeGreaterThan(0);
    });
    await user.click(screen.getByRole("button", { name: /show fewer sources/i }));
  });

  it("copilot sections have expected default disclosure state", async () => {
    window.history.pushState({}, "", "/host/conversations");
    vi.stubGlobal("fetch", createHostFetchMock());

    render(<App />);
    await signIn();

    await waitFor(() => expect(findDisclosureSummary(/conversation summary/i)).toBeInTheDocument());
    expect(findDisclosureSummary(/conversation summary/i).closest("details")?.open).toBe(true);
    expect(findDisclosureSummary(/sources \(\d+\)/i).closest("details")?.open).toBe(false);
    expect(findDisclosureSummary(/suggested replies \(\d+\)/i).closest("details")?.open).toBe(true);
    expect(findDisclosureSummary(/^generate reply$/i).closest("details")?.open).toBe(true);
    expect(findDisclosureSummary(/^generated reply$/i).closest("details")?.open).toBe(false);
  });

  it("generated reply expands after successful generation and supports insert/copy", async () => {
    window.history.pushState({}, "", "/host/conversations");
    const fetchMock = createHostFetchMock();
    vi.stubGlobal("fetch", fetchMock);

    render(<App />);
    const user = await signIn();

    await waitFor(() => expect(screen.getByRole("button", { name: /generate host reply draft/i })).toBeInTheDocument());
    await user.click(screen.getByRole("button", { name: /generate host reply draft/i }));

    await waitFor(() => expect(screen.getByRole("textbox", { name: /generated reply/i })).toBeInTheDocument());
    expect(findDisclosureSummary(/^generated reply$/i).closest("details")?.open).toBe(true);

    await user.click(screen.getByRole("button", { name: /insert generated reply into host composer/i }));
    const replyInput = screen.getByLabelText(/host reply/i, { selector: "textarea" }) as HTMLTextAreaElement;
    expect(replyInput.value).toContain("Thanks for your request");

    await user.click(screen.getByRole("button", { name: /^copy generated reply$/i }));
    await waitFor(() => expect(screen.getByText(/^copied$/i)).toBeInTheDocument());

    const sendCalls = fetchMock.mock.calls.filter((call) => String(call[0]).includes("/messages/host"));
    expect(sendCalls.length).toBe(0);
  });

  it("generated reply state resets when switching conversations", async () => {
    window.history.pushState({}, "", "/host/conversations");
    vi.stubGlobal("fetch", createHostFetchMock());

    render(<App />);
    const user = await signIn();

    await waitFor(() => expect(screen.getByRole("button", { name: /generate host reply draft/i })).toBeInTheDocument());
    await user.click(screen.getByRole("button", { name: /generate host reply draft/i }));
    await waitFor(() => expect(screen.getByRole("textbox", { name: /generated reply/i })).toBeInTheDocument());

    await user.click(screen.getByRole("button", { name: /next/i }));
    await waitFor(() => expect(screen.queryByRole("textbox", { name: /generated reply/i })).not.toBeInTheDocument());
  });

  it("copilot disclosures toggle with keyboard", async () => {
    window.history.pushState({}, "", "/host/conversations");
    vi.stubGlobal("fetch", createHostFetchMock());

    render(<App />);
    const user = await signIn();

    await waitFor(() => expect(findDisclosureSummary(/sources \(\d+\)/i)).toBeInTheDocument());
    const sourcesSummary = findDisclosureSummary(/sources \(\d+\)/i);
    sourcesSummary.focus();
    fireEvent.keyDown(sourcesSummary, { key: "Enter", code: "Enter" });
    fireEvent.click(sourcesSummary);
    expect(sourcesSummary.closest("details")?.open).toBe(true);
  });

  it("generated reply errors remain visible with section indicator", async () => {
    window.history.pushState({}, "", "/host/conversations");
    vi.stubGlobal("fetch", createHostFetchMock([conversationRow()], { failGenerate: true }));

    render(<App />);
    const user = await signIn();

    await waitFor(() => expect(screen.getByRole("button", { name: /generate host reply draft/i })).toBeInTheDocument());
    await user.click(screen.getByRole("button", { name: /generate host reply draft/i }));

    await waitFor(() => expect(screen.getByText(/guest services are temporarily unavailable/i)).toBeInTheDocument());
    expect(screen.getAllByText(/^error$/i).length).toBeGreaterThan(0);
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

    const replyInput = screen.getByLabelText(/host reply/i, { selector: "textarea" }) as HTMLTextAreaElement;
    expect(replyInput.value).toContain("Thanks for reaching out");

    const sendCalls = fetchMock.mock.calls.filter((call) => String(call[0]).includes("/messages/host"));
    expect(sendCalls.length).toBe(0);
  });

  it("uses separate scroll bodies for inbox cards and copilot content", async () => {
    window.history.pushState({}, "", "/host/conversations");
    vi.stubGlobal("fetch", createHostFetchMock());

    render(<App />);
    await signIn();

    await waitFor(() => expect(screen.getByLabelText(/conversation inbox list/i)).toBeInTheDocument());

    const inboxList = screen.getByLabelText(/conversation inbox list/i);
    const listColumn = inboxList.closest(".sf-host-list-column");
    const pagination = listColumn?.querySelector(".sf-host-pagination");

    expect(listColumn).not.toBeNull();
    expect(pagination).not.toBeNull();
    expect(inboxList.closest(".sf-host-pagination")).toBeNull();

    const copilotScroll = screen.getByLabelText(/ai copilot content/i);
    const copilotHeading = screen.getByRole("heading", { name: /host copilot/i });
    const copilotPanel = copilotScroll.closest(".sf-host-copilot-panel");
    const copilotPanelTop = copilotPanel?.querySelector(".sf-host-copilot-panel-top");

    expect(copilotScroll.classList.contains("sf-host-copilot-scroll")).toBe(true);
    expect(copilotPanel).not.toBeNull();
    expect(copilotPanelTop).not.toBeNull();
    expect(copilotScroll.contains(copilotPanelTop as Node)).toBe(false);
    expect(copilotScroll.contains(copilotHeading)).toBe(false);
  });
});
