import { render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { DemoPage, resolveDemoReservationId } from "../src/pages/DemoPage";

vi.mock("../src/components", () => ({
  StayFlowChatWidget: ({ reservationId }: { reservationId?: string }) => (
    <div data-testid="stayflow-chat-widget">{reservationId ?? "(none)"}</div>
  )
}));

describe("DemoPage reservation selection", () => {
  beforeEach(() => {
    window.history.pushState({}, "", "/demo");
    vi.stubEnv("VITE_DEMO_RESERVATION_ID", "55555555-5555-5555-5555-555555555555");
    vi.stubEnv("VITE_DEMO_PROPERTY_ID", "22222222-2222-2222-2222-222222222222");
    vi.stubEnv("VITE_DEMO_GUEST_ID", "44444444-4444-4444-4444-444444444444");
    vi.unstubAllGlobals();
  });

  it("resolves the seeded payment demo reservation from the query string", () => {
    window.history.pushState({}, "", "/demo?reservation=DEMO-PAY-002");

    expect(resolveDemoReservationId()).toBe("55555555-5555-5555-5555-555555555556");
  });

  it("uses the configured fallback reservation when no override is specified", () => {
    expect(resolveDemoReservationId()).toBe("55555555-5555-5555-5555-555555555555");
  });

  it("passes the resolved reservation into the guest widget", () => {
    window.history.pushState({}, "", "/demo?reservation=DEMO-PAY-002");

    render(<DemoPage />);

    expect(screen.getByTestId("stayflow-chat-widget")).toHaveTextContent("55555555-5555-5555-5555-555555555556");
  });
});
