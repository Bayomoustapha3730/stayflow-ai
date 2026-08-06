import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";

const hostAuthState = {
  accessToken: "token",
  currentUser: {
    id: "u1",
    companyId: "c1",
    fullName: "Owner",
    email: "owner@test",
    phoneNumber: "+254700000001",
    preferredLanguage: "en",
    timeZone: "Africa/Nairobi",
    isEmailVerified: true,
    emailNotificationsEnabled: true,
    securityNotificationsEnabled: true,
    productUpdatesEnabled: true,
    organizationRole: "Owner",
    roles: ["Owner"],
    permissions: []
  },
  isAuthenticated: true,
  isSigningIn: false,
  error: null,
  login: vi.fn(),
  logout: vi.fn(),
  clearError: vi.fn(),
  refreshCurrentUser: vi.fn(),
  setCurrentUserProfile: vi.fn()
};

const changePlan = vi.fn().mockResolvedValue(undefined);

vi.mock("../src/hooks/useHostAuth", () => ({
  useHostAuth: () => hostAuthState
}));

vi.mock("../src/hooks/useBillingDashboard", () => ({
  useBillingDashboard: () => ({
    subscription: {
      companyId: "c1",
      status: "Active",
      cancelAtPeriodEnd: false,
      currentPeriodStartUtc: "2026-08-01T00:00:00Z",
      currentPeriodEndUtc: "2026-09-01T00:00:00Z",
      trialEndsAtUtc: "2026-08-20T00:00:00Z",
      planName: "Starter"
    },
    invoices: [],
    usage: {
      companyId: "c1",
      generatedAtUtc: "2026-08-10T00:00:00Z",
      metrics: [
        {
          metric: "AiRequests",
          entitlementKey: "ai.requests",
          used: 140,
          limit: 1000,
          remaining: 860,
          isUnlimited: false,
          unit: "requests",
          periodStartUtc: "2026-08-01T00:00:00Z",
          periodEndUtc: "2026-09-01T00:00:00Z"
        }
      ]
    },
    isLoading: false,
    isMutating: false,
    error: null,
    message: null,
    refresh: vi.fn(),
    openCheckout: vi.fn(),
    openBillingPortal: vi.fn(),
    openPaymentMethodPortal: vi.fn(),
    changePlan,
    cancelSubscription: vi.fn(),
    resumeSubscription: vi.fn()
  })
}));

import { BillingDashboardPage } from "../src/pages/BillingDashboardPage";

describe("BillingDashboardPage", () => {
  it("renders billing overview and usage", () => {
    render(<BillingDashboardPage />);

    expect(screen.getByText("Billing & Subscription")).toBeInTheDocument();
    expect(screen.getByText("Current Subscription")).toBeInTheDocument();
    expect(screen.getByText("Usage Summary")).toBeInTheDocument();
  });

  it("invokes plan change action", () => {
    render(<BillingDashboardPage />);

    fireEvent.click(screen.getAllByRole("button", { name: /(Upgrade|Downgrade|Change) Plan/ })[0]);
    fireEvent.click(screen.getByRole("button", { name: /Confirm (Upgrade|Downgrade|Change)/ }));

    expect(changePlan).toHaveBeenCalled();
  });
});
