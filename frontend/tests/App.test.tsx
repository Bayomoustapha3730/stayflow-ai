import { render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";

vi.mock("../src/pages/DemoPage", () => ({
  DemoPage: () => <div data-testid="demo-page">demo</div>
}));

vi.mock("../src/pages/AccountSettingsPage", () => ({
  AccountSettingsPage: () => <div data-testid="account-settings-page">account</div>
}));

vi.mock("../src/pages/BillingDashboardPage", () => ({
  BillingDashboardPage: () => <div data-testid="billing-dashboard-page">billing</div>
}));

vi.mock("../src/pages/ForgotPasswordPage", () => ({
  ForgotPasswordPage: () => <div data-testid="forgot-password-page">forgot</div>
}));

vi.mock("../src/pages/HostInboxPage", () => ({
  HostInboxPage: () => <div data-testid="host-inbox-page">host</div>
}));

vi.mock("../src/pages/InvitationDecisionPage", () => ({
  InvitationDecisionPage: () => <div data-testid="invitation-decision-page">invite</div>
}));

vi.mock("../src/pages/HostCopilotWorkspacePage", () => ({
  HostCopilotWorkspacePage: () => <div data-testid="host-copilot-workspace-page">host-copilot</div>
}));

vi.mock("../src/pages/PlatformAdminPage", () => ({
  PlatformAdminPage: () => <div data-testid="platform-admin-page">platform-admin</div>
}));

vi.mock("../src/pages/PropertyKnowledgePage", () => ({
  PropertyKnowledgePage: ({ propertyId }: { propertyId: string | null }) => (
    <div data-testid="property-knowledge-page">{propertyId ?? "(none)"}</div>
  )
}));

vi.mock("../src/pages/ResetPasswordPage", () => ({
  ResetPasswordPage: () => <div data-testid="reset-password-page">reset</div>
}));

vi.mock("../src/pages/VerifyEmailPage", () => ({
  VerifyEmailPage: () => <div data-testid="verify-email-page">verify</div>
}));

vi.mock("../src/pages/WhatsAppSettingsPage", () => ({
  WhatsAppSettingsPage: () => <div data-testid="whatsapp-settings-page">whatsapp-settings</div>
}));

vi.mock("../src/pages/OrganizationSettingsPage", () => ({
  OrganizationSettingsPage: () => <div data-testid="organization-settings-page">organization-settings</div>
}));

vi.mock("../src/pages/MyOrganizationsPage", () => ({
  MyOrganizationsPage: () => <div data-testid="my-organizations-page">my-organizations</div>
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

  it("renders the account settings page for account route", () => {
    window.history.pushState({}, "", "/host/settings/account");

    render(<App />);

    expect(screen.getByTestId("account-settings-page")).toBeInTheDocument();
  });

  it("renders the billing dashboard for billing route", () => {
    window.history.pushState({}, "", "/host/settings/billing");

    render(<App />);

    expect(screen.getByTestId("billing-dashboard-page")).toBeInTheDocument();
  });

  it("renders the my organizations page for host organizations route", () => {
    window.history.pushState({}, "", "/host/organizations");

    render(<App />);

    expect(screen.getByTestId("my-organizations-page")).toBeInTheDocument();
  });

  it("renders the forgot password page for auth route", () => {
    window.history.pushState({}, "", "/auth/forgot-password");

    render(<App />);

    expect(screen.getByTestId("forgot-password-page")).toBeInTheDocument();
  });

  it("renders the reset password page for auth route", () => {
    window.history.pushState({}, "", "/auth/reset-password?token=abc");

    render(<App />);

    expect(screen.getByTestId("reset-password-page")).toBeInTheDocument();
  });

  it("renders the verify email page for auth route", () => {
    window.history.pushState({}, "", "/auth/verify-email?token=abc");

    render(<App />);

    expect(screen.getByTestId("verify-email-page")).toBeInTheDocument();
  });

  it("renders the invitation decision page for invitation route", () => {
    window.history.pushState({}, "", "/invitation/respond?token=abc");

    render(<App />);

    expect(screen.getByTestId("invitation-decision-page")).toBeInTheDocument();
  });

  it("renders the host copilot workspace route", () => {
    window.history.pushState({}, "", "/host/copilot");

    render(<App />);

    expect(screen.getByTestId("host-copilot-workspace-page")).toBeInTheDocument();
  });

  it("renders the platform admin route", () => {
    window.history.pushState({}, "", "/platform-admin");

    render(<App />);

    expect(screen.getByTestId("platform-admin-page")).toBeInTheDocument();
  });

  it("renders the platform admin route with trailing slash", () => {
    window.history.pushState({}, "", "/platform-admin/");

    render(<App />);

    expect(screen.getByTestId("platform-admin-page")).toBeInTheDocument();
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

    expect(screen.getByTestId("onboarding-page")).toHaveTextContent("root");
  });
});
