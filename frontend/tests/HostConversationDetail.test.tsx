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

function detail(status = ConversationStatus.HumanManaged, humanTakeoverEnabled = true) {
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
      status: 0
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
});
