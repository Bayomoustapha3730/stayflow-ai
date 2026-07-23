import { render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { HostInboxHeader } from "../src/components/host/HostInboxHeader";

function renderHeader(connectionStatus: "live" | "reconnecting" | "degraded" | "offline") {
  render(
    <HostInboxHeader
      isRefreshing={false}
      connectionStatus={connectionStatus}
      totalUnreadCount={3}
      notificationsEnabled={false}
      notificationsSupported={false}
      onRefresh={vi.fn()}
      onEnableNotifications={vi.fn()}
      onSignOut={vi.fn()}
    />
  );
}

describe("HostInboxHeader", () => {
  it("shows Live when realtime is connected", () => {
    renderHeader("live");
    expect(screen.getByText(/^live$/i)).toBeInTheDocument();
  });

  it("shows Reconnecting when reconnect attempt is active", () => {
    renderHeader("reconnecting");
    expect(screen.getByText(/^reconnecting$/i)).toBeInTheDocument();
  });

  it("shows degraded connected wording when signalr is unavailable but http works", () => {
    renderHeader("degraded");
    expect(screen.getByText(/connected - updates may be delayed/i)).toBeInTheDocument();
  });

  it("shows Offline only when workspace is fully unavailable", () => {
    renderHeader("offline");
    expect(screen.getByText(/^offline$/i)).toBeInTheDocument();
  });
});
