import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { HostConversationHeader } from "../src/components/host/HostConversationHeader";
import { HostConversationListItem } from "../src/components/host/HostConversationListItem";
import { HostConversationMessage } from "../src/components/host/HostConversationMessage";
import { ConversationMessageType, ConversationSenderType, ConversationStatus, GuestChannel } from "../src/models/enums";
import { ConversationMessageDeliveryStatus } from "../src/models/messageDelivery";

describe("WhatsApp host rendering", () => {
  it("shows the WhatsApp channel badge and masked phone fallback", () => {
    render(
      <HostConversationHeader
        conversation={{
          id: "c-1",
          conversationId: "c-1",
          guestId: "g-1",
          reservationId: "r-1",
          propertyId: "p-1",
          status: ConversationStatus.HumanManaged,
          channel: GuestChannel.WhatsApp,
          channelIdentity: "+14155551234",
          subject: "Check-in",
          guest: {
            id: "g-1",
            firstName: "Ada",
            lastName: "Lovelace",
            fullName: "Ada Lovelace",
            email: null,
            maskedPhoneNumber: "+1******1234",
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
          assignedUser: null,
          humanTakeoverEnabled: true,
          requiresHostAttention: true,
          escalationReason: null,
          startedAt: "2026-07-22T10:00:00Z",
          lastActivityAt: "2026-07-22T11:00:00Z",
          closedAt: null,
          latestVisibleMessagePreview: "Hello",
          latestVisibleMessageSenderType: ConversationSenderType.Guest,
          latestVisibleMessageTimestamp: "2026-07-22T11:00:00Z",
          totalVisibleMessageCount: 2,
          unreadMessageCount: 1,
          lastReadAt: null,
          messages: []
        }}
        isRefreshing={false}
        onRefresh={() => {}}
      />
    );

    expect(screen.getByText("WhatsApp")).toBeInTheDocument();
    expect(screen.getByText("+1******1234")).toBeInTheDocument();
  });

  it("shows WhatsApp as a list pill for inbox rows", () => {
    render(
      <HostConversationListItem
        item={{
          id: "c-1",
          conversationId: "c-1",
          guestId: "g-1",
          reservationId: "r-1",
          propertyId: "p-1",
          status: ConversationStatus.AwaitingHost,
          channel: GuestChannel.WhatsApp,
          channelIdentity: "+14155551234",
          subject: "Check-in",
          guest: {
            id: "g-1",
            firstName: "Ada",
            lastName: "Lovelace",
            fullName: "Ada Lovelace",
            email: null,
            maskedPhoneNumber: "+1******1234",
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
          assignedUser: null,
          humanTakeoverEnabled: true,
          requiresHostAttention: true,
          escalationReason: null,
          startedAt: "2026-07-22T10:00:00Z",
          lastActivityAt: "2026-07-22T11:00:00Z",
          closedAt: null,
          latestVisibleMessagePreview: "Hello",
          latestVisibleMessageSenderType: ConversationSenderType.Guest,
          latestVisibleMessageTimestamp: "2026-07-22T11:00:00Z",
          totalVisibleMessageCount: 2,
          unreadMessageCount: 1,
          lastReadAt: null
        }}
        isSelected={false}
        onSelect={() => {}}
      />
    );

    expect(screen.getByText("WhatsApp")).toBeInTheDocument();
    expect(screen.getByText("+1******1234")).toBeInTheDocument();
  });

  it("shows outbound delivery states and hides them for inbound guest messages", () => {
    const { rerender } = render(
      <HostConversationMessage
        message={{
          id: "m-1",
          conversationId: "c-1",
          senderType: ConversationSenderType.Host,
          messageType: ConversationMessageType.Text,
          content: "Reply",
          isInternal: false,
          sentAt: "2026-07-22T10:05:00Z",
          deliveryStatus: ConversationMessageDeliveryStatus.Read,
          provider: 1
        }}
      />
    );

    expect(screen.getByText("Read")).toBeInTheDocument();

    rerender(
      <HostConversationMessage
        message={{
          id: "m-2",
          conversationId: "c-1",
          senderType: ConversationSenderType.Guest,
          messageType: ConversationMessageType.Text,
          content: "Inbound",
          isInternal: false,
          sentAt: "2026-07-22T10:06:00Z",
          deliveryStatus: ConversationMessageDeliveryStatus.Delivered,
          provider: 1
        }}
      />
    );

    expect(screen.queryByText("Delivered")).not.toBeInTheDocument();
    expect(screen.queryByText("m-2")).not.toBeInTheDocument();
  });
});