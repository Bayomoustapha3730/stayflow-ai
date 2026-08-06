import { render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";

vi.mock("../src/pages/DemoPage", () => ({
  DemoPage: () => <div data-testid="demo-page">demo</div>
}));

vi.mock("../src/pages/HostInboxPage", () => ({
  HostInboxPage: () => <div data-testid="host-inbox-page">host</div>
}));

vi.mock("../src/pages/HostCopilotWorkspacePage", () => ({
  HostCopilotWorkspacePage: () => <div data-testid="host-copilot-workspace-page">host-copilot</div>
}));

vi.mock("../src/pages/PropertyKnowledgePage", () => ({
  PropertyKnowledgePage: ({ propertyId }: { propertyId: string | null }) => (
    <div data-testid="property-knowledge-page">{propertyId ?? "(none)"}</div>
  )
}));

vi.mock("../src/pages/WhatsAppSettingsPage", () => ({
  WhatsAppSettingsPage: () => <div data-testid="whatsapp-settings-page">whatsapp-settings</div>
}));

vi.mock("../src/pages/OrganizationSettingsPage", () => ({
  OrganizationSettingsPage: () => <div data-testid="organization-settings-page">organization-settings</div>
}));

vi.mock("../src/pages/OnboardingPage", () => ({
  OnboardingPage: ({ routeStep }: { routeStep?: string }) => <div data-testid="onboarding-page">{routeStep ?? "root"}</div>
}));

import App from "../src/App";

describe("App routing", () => {
  it("renders the host inbox for conversation routes", () => {
    window.history.pushState({}, "", "/host/conversations");

    render(<App />);

    expect(screen.getByTestId("host-inbox-page")).toBeInTheDocument();
  });

  it("renders the property knowledge page for knowledge routes", () => {
    window.history.pushState({}, "", "/host/properties/demo-property/knowledge");

    render(<App />);

    expect(screen.getByTestId("property-knowledge-page")).toHaveTextContent("demo-property");
  });

  it("renders the property knowledge page for the host properties index route", () => {
    vi.stubEnv("VITE_DEMO_PROPERTY_ID", "22222222-2222-4222-8222-222222222222");
    window.history.pushState({}, "", "/host/properties/");

    render(<App />);

    expect(screen.getByTestId("property-knowledge-page")).toHaveTextContent("22222222-2222-4222-8222-222222222222");
  });

  it("renders the WhatsApp settings page for host settings route", () => {
    window.history.pushState({}, "", "/host/settings/whatsapp");

    render(<App />);

    expect(screen.getByTestId("whatsapp-settings-page")).toBeInTheDocument();
  });

  it("renders the host copilot workspace route", () => {
    window.history.pushState({}, "", "/host/copilot");

    render(<App />);

    expect(screen.getByTestId("host-copilot-workspace-page")).toBeInTheDocument();
  });

  it("passes a null property ID when no valid demo fallback is configured", () => {
    vi.stubEnv("VITE_DEMO_PROPERTY_ID", "");
    window.history.pushState({}, "", "/host/properties/");

    render(<App />);

    expect(screen.getByTestId("property-knowledge-page")).toHaveTextContent("(none)");
  });

  it("falls back to the demo page for non-host routes", () => {
    window.history.pushState({}, "", "/guest/demo");

    render(<App />);

    expect(screen.getByTestId("demo-page")).toBeInTheDocument();
  });

  it("renders onboarding root route", () => {
    window.history.pushState({}, "", "/onboarding");

    render(<App />);

    expect(screen.getByTestId("onboarding-page")).toHaveTextContent("root");
  });

  it("renders onboarding step route", () => {
    window.history.pushState({}, "", "/onboarding/property");

    render(<App />);

    expect(screen.getByTestId("onboarding-page")).toHaveTextContent("property");
  });

  it("renders get-started route", () => {
    window.history.pushState({}, "", "/get-started");

    render(<App />);

    expect(screen.getByTestId("onboarding-page")).toHaveTextContent("completed");
  });
});
