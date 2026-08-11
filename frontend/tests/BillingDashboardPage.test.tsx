import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { useBillingDashboard } from "../src/hooks/useBillingDashboard";

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
  useBillingDashboard: vi.fn(() => ({
    subscription: {
      companyId: "c1",
      status: "Active",
      cancelAtPeriodEnd: false,
      currentPeriodStartUtc: "2026-08-01T00:00:00Z",
      currentPeriodEndUtc: "2026-09-01T00:00:00Z",
      trialEndsAtUtc: "2026-08-20T00:00:00Z",
      planName: "Starter",
      canStartCheckout: true,
      canOpenBillingPortal: true,
      canManagePaymentMethod: true,
      canCancel: true,
      canResume: false,
      hasStripeCustomer: true,
      hasStripeSubscription: true
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
  }))
}));

import { BillingDashboardPage } from "../src/pages/BillingDashboardPage";

describe("BillingDashboardPage", () => {
  it("renders billing overview and usage", () => {
    render(<BillingDashboardPage />);

    expect(screen.getByText("Billing & Subscription")).toBeInTheDocument();
    expect(screen.getByText("Current Subscription")).toBeInTheDocument();
    expect(screen.getByText("Usage Summary")).toBeInTheDocument();
  });

  it("invokes plan change action", async () => {
    render(<BillingDashboardPage />);

    fireEvent.click(screen.getAllByRole("button", { name: /(Upgrade|Downgrade|Change) Plan/ })[0]);
    fireEvent.click(screen.getByRole("button", { name: /Confirm (Upgrade|Downgrade|Change)/ }));

    await waitFor(() => {
      expect(changePlan).toHaveBeenCalled();
    });
  });

  it("hides portal and cancellation controls for free tenants that have no Stripe relationship", () => {
    const freeTenantSubscription = {
      companyId: "c1",
      status: "Active",
      cancelAtPeriodEnd: false,
      currentPeriodStartUtc: "2026-08-01T00:00:00Z",
      currentPeriodEndUtc: "2026-09-01T00:00:00Z",
      trialEndsAtUtc: null,
      planName: "Free",
      canStartCheckout: true,
      canOpenBillingPortal: false,
      canManagePaymentMethod: false,
      canCancel: false,
      canResume: false,
      hasStripeCustomer: false,
      hasStripeSubscription: false
    };

    vi.mocked(useBillingDashboard).mockReturnValueOnce({
      subscription: freeTenantSubscription,
      invoices: [],
      usage: null,
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
    });

    render(<BillingDashboardPage />);

    expect(screen.queryByRole("button", { name: /Open Billing Portal/i })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /Manage Payment Method/i })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /Cancel at Period End/i })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /Resume/i })).not.toBeInTheDocument();
  });
});
