import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { HostConversationDetail } from "../src/components/host/HostConversationDetail";
import {
  ConversationMessageType,
  ConversationSenderType,
  ConversationStatus,
  GuestChannel
} from "../src/models/enums";

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

function fail(status: number, message: string) {
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

function detail(
  status = ConversationStatus.HumanManaged,
  humanTakeoverEnabled = true,
  lifecycleStage?: string | null
) {
  return {
    id: "c-1",
    conversationId: "c-1",
    guestId: "g-1",
    reservationId: "r-1",
    propertyId: "p-1",
    status,
    channel: GuestChannel.Web,
    channelIdentity: null,
    subject: "Check-in",
    guest: {
      id: "g-1",
      firstName: "Ada",
      lastName: "Lovelace",
      fullName: "Ada Lovelace",
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
      status: 0,
      lifecycleStage
    },
    assignedUser: {
      id: "u-1",
      fullName: "Front Desk"
    },
    humanTakeoverEnabled,
    requiresHostAttention: true,
    escalationReason: null,
    startedAt: "2026-07-22T10:00:00Z",
    lastActivityAt: "2026-07-22T11:00:00Z",
    closedAt: status === ConversationStatus.Closed ? "2026-07-22T11:30:00Z" : null,
    latestVisibleMessagePreview: "Can I check out late?",
    latestVisibleMessageSenderType: ConversationSenderType.Guest,
    latestVisibleMessageTimestamp: "2026-07-22T11:00:00Z",
    totalVisibleMessageCount: 4,
    messages: []
  };
}

function history() {
  return {
    conversationId: "c-1",
    messages: {
      items: [
        {
          id: "m-1",
          conversationId: "c-1",
          senderType: ConversationSenderType.Guest,
          messageType: ConversationMessageType.Text,
          content: "Guest asks for late checkout",
          isInternal: false,
          sentAt: "2026-07-22T10:00:00Z"
        },
        {
          id: "m-2",
          conversationId: "c-1",
          senderType: ConversationSenderType.AI,
          messageType: ConversationMessageType.Text,
          content: "AI response",
          isInternal: false,
          sentAt: "2026-07-22T10:01:00Z"
        },
        {
          id: "m-3",
          conversationId: "c-1",
          senderType: ConversationSenderType.Host,
          messageType: ConversationMessageType.Text,
          content: "Host response",
          isInternal: false,
          sentAt: "2026-07-22T10:02:00Z"
        },
        {
          id: "m-4",
          conversationId: "c-1",
          senderType: ConversationSenderType.System,
          messageType: ConversationMessageType.InternalNote,
          content: "Staff note only",
          isInternal: true,
          sentAt: "2026-07-22T10:03:00Z"
        }
      ],
      pageNumber: 1,
      pageSize: 100,
      totalCount: 4,
      totalPages: 1
    }
  };
}

function createFetchMock(nextDetail = detail()) {
  return vi.fn().mockImplementation((url: string) => {
    if (url.includes("/messages/host")) {
      return Promise.resolve(
        ok({
          id: "m-5",
          conversationId: "c-1",
          senderType: ConversationSenderType.Host,
          messageType: ConversationMessageType.Text,
          content: "Reply",
          isInternal: false,
          sentAt: "2026-07-22T10:05:00Z"
        })
      );
    }

    if (url.includes("/messages")) {
      return Promise.resolve(ok(history()));
    }

    return Promise.resolve(ok(nextDetail));
  });
}

describe("HostConversationDetail", () => {
  beforeEach(() => {
    vi.stubEnv("VITE_STAYFLOW_API_URL", "http://test.local");
  });

  it("renders loading then timeline with guest, AI, host, and internal-note labels", async () => {
    vi.stubGlobal("fetch", createFetchMock());

    render(
      <HostConversationDetail
        conversationId="c-1"
        accessToken="host-token"
        onUnauthorized={vi.fn()}
      />
    );

    expect(screen.getByLabelText(/loading conversation detail/i)).toBeInTheDocument();

    await waitFor(() => expect(screen.getByRole("heading", { name: /timeline/i })).toBeInTheDocument());
    expect(screen.getByText("Guest")).toBeInTheDocument();
    expect(screen.getByText("AI")).toBeInTheDocument();
    expect(screen.getByText("Host")).toBeInTheDocument();
    expect(screen.getByText("Internal Note")).toBeInTheDocument();
  });

  it("renders detail failure and supports retry", async () => {
    let failedOnce = false;
    const fetchMock = vi.fn().mockImplementation((url: string) => {
      if (url.includes("/messages")) {
        return Promise.resolve(ok(history()));
      }

      if (!failedOnce) {
        failedOnce = true;
        return Promise.resolve(fail(500, "Server error"));
      }

      return Promise.resolve(ok(detail()));
    });

    vi.stubGlobal("fetch", fetchMock);

    render(
      <HostConversationDetail
        conversationId="c-1"
        accessToken="host-token"
        onUnauthorized={vi.fn()}
      />
    );

    await waitFor(() => expect(screen.getByRole("heading", { name: /unable to load conversation/i })).toBeInTheDocument());

    await userEvent.click(screen.getByRole("button", { name: /retry/i }));

    await waitFor(() => expect(screen.getByRole("heading", { name: /timeline/i })).toBeInTheDocument());
  });

  it("rejects empty replies and sends with Ctrl+Enter", async () => {
    const fetchMock = createFetchMock();
    vi.stubGlobal("fetch", fetchMock);

    render(
      <HostConversationDetail
        conversationId="c-1"
        accessToken="host-token"
        onUnauthorized={vi.fn()}
      />
    );

    await waitFor(() => expect(screen.getByRole("textbox", { name: /^host reply$/i })).toBeInTheDocument());

    const sendButton = screen.getByRole("button", { name: /send host reply/i });
    expect(sendButton).toBeDisabled();

    const replyInput = screen.getByRole("textbox", { name: /^host reply$/i });
    await userEvent.type(replyInput, "Reply with details");
    fireEvent.keyDown(replyInput, { key: "Enter", code: "Enter", ctrlKey: true });

    await waitFor(() => {
      const urls = fetchMock.mock.calls.map((call) => String(call[0]));
      expect(urls.some((url) => url.includes("/messages/host"))).toBe(true);
    });
  });

  it("closed conversation disables reply and note composers", async () => {
    vi.stubGlobal("fetch", createFetchMock(detail(ConversationStatus.Closed, false)));

    render(
      <HostConversationDetail
        conversationId="c-1"
        accessToken="host-token"
        onUnauthorized={vi.fn()}
      />
    );

    await waitFor(() => expect(screen.getByRole("textbox", { name: /^host reply$/i })).toBeDisabled());
    expect(screen.getByRole("textbox", { name: /note content/i })).toBeDisabled();
  });

  it("shows a disabled return to AI action when the conversation is already AI-managed", async () => {
    vi.stubGlobal("fetch", createFetchMock(detail(ConversationStatus.Open, false)));

    render(
      <HostConversationDetail
        conversationId="c-1"
        accessToken="host-token"
        onUnauthorized={vi.fn()}
      />
    );

    await waitFor(() =>
      expect(screen.getByRole("button", { name: /return conversation to ai/i })).toBeDisabled()
    );
    expect(screen.getByRole("button", { name: /return conversation to ai/i })).toHaveTextContent(/ai already active/i);
  });

  it("calls onUnauthorized when API returns 401", async () => {
    const onUnauthorized = vi.fn();
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(fail(401, "Unauthorized")));

    render(
      <HostConversationDetail
        conversationId="c-1"
        accessToken="host-token"
        onUnauthorized={onUnauthorized}
      />
    );

    await waitFor(() => expect(onUnauthorized).toHaveBeenCalled());
  });

  it("keeps timeline in its own scroll section and composers outside timeline", async () => {
    vi.stubGlobal("fetch", createFetchMock());

    const { container } = render(
      <HostConversationDetail
        conversationId="c-1"
        accessToken="host-token"
        onUnauthorized={vi.fn()}
      />
    );

    await waitFor(() => expect(screen.getByRole("heading", { name: /timeline/i })).toBeInTheDocument());

    const workspace = container.querySelector(".sf-host-detail-workspace");
    const timelineSection = container.querySelector(".sf-host-conversation-timeline-section");
    const timeline = container.querySelector(".sf-host-timeline");
    const composerStack = container.querySelector(".sf-host-composer-stack");
    const replyInput = screen.getByRole("textbox", { name: /^host reply$/i });
    const noteInput = screen.getByRole("textbox", { name: /note content/i });

    expect(workspace).not.toBeNull();
    expect(timelineSection).not.toBeNull();
    expect(timeline).not.toBeNull();
    expect(composerStack).not.toBeNull();

    expect(workspace?.classList.contains("sf-host-detail-workspace")).toBe(true);
    expect(timeline?.classList.contains("sf-host-timeline")).toBe(true);
    expect(composerStack?.classList.contains("sf-host-composer-stack")).toBe(true);
    expect(timelineSection?.contains(timeline as Node)).toBe(true);
    expect(workspace?.contains(timelineSection as Node)).toBe(true);
    expect(workspace?.contains(composerStack as Node)).toBe(true);
    expect(timelineSection?.contains(composerStack as Node)).toBe(false);
    expect(timeline?.contains(replyInput)).toBe(false);
    expect(timeline?.contains(noteInput)).toBe(false);
    expect(composerStack?.contains(replyInput)).toBe(true);
    expect(composerStack?.contains(noteInput)).toBe(true);
  });

  describe("reservation lifecycle badge", () => {
    const cases: Array<[string, string]> = [
      ["NotConfirmed", "NOT CONFIRMED"],
      ["FutureConfirmed", "FUTURE"],
      ["PreArrival", "PRE-ARRIVAL"],
      ["ArrivingToday", "ARRIVING TODAY"],
      ["InStay", "IN STAY"],
      ["CheckingOutToday", "CHECKING OUT TODAY"],
      ["Completed", "COMPLETED"],
      ["Cancelled", "CANCELLED"],
      ["NoShow", "NO SHOW"]
    ];

    it.each(cases)("renders %s as %s", async (lifecycleStage, expectedLabel) => {
      vi.stubGlobal("fetch", createFetchMock(detail(ConversationStatus.HumanManaged, true, lifecycleStage)));

      render(
        <HostConversationDetail
          conversationId="c-1"
          accessToken="host-token"
          onUnauthorized={vi.fn()}
        />
      );

      await waitFor(() => expect(screen.getByText(expectedLabel)).toBeInTheDocument());
    });

    it("omits the badge when lifecycle stage is missing", async () => {
      vi.stubGlobal("fetch", createFetchMock(detail(ConversationStatus.HumanManaged, true, null)));

      render(
        <HostConversationDetail
          conversationId="c-1"
          accessToken="host-token"
          onUnauthorized={vi.fn()}
        />
      );

      await waitFor(() => expect(screen.getByText("ABC123")).toBeInTheDocument());
      expect(screen.queryByText("NOT CONFIRMED")).not.toBeInTheDocument();
      expect(screen.queryByText("IN STAY")).not.toBeInTheDocument();
    });

    it("omits the badge for an unrecognized lifecycle stage without crashing", async () => {
      vi.stubGlobal("fetch", createFetchMock(detail(ConversationStatus.HumanManaged, true, "SomeFutureStage")));

      render(
        <HostConversationDetail
          conversationId="c-1"
          accessToken="host-token"
          onUnauthorized={vi.fn()}
        />
      );

      await waitFor(() => expect(screen.getByText("ABC123")).toBeInTheDocument());
      expect(screen.queryByText("SomeFutureStage")).not.toBeInTheDocument();
    });

    it("still renders the M-PESA payment summary alongside the lifecycle badge", async () => {
      vi.stubGlobal("fetch", createFetchMock(detail(ConversationStatus.HumanManaged, true, "InStay")));

      render(
        <HostConversationDetail
          conversationId="c-1"
          accessToken="host-token"
          onUnauthorized={vi.fn()}
        />
      );

      await waitFor(() => expect(screen.getByText("IN STAY")).toBeInTheDocument());
      expect(screen.getByRole("heading", { name: "M-PESA" })).toBeInTheDocument();
    });
  });
});
