import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
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
      hasStripeSubscription: true,
      capability: {
        provider: "Stripe",
        stripeConfigured: true,
        checkoutAvailable: true,
        portalAvailable: true,
        paymentMethodManagementAvailable: true,
        message: "Stripe billing is configured.",
        missingConfiguration: []
      }
    },
    paymentOptions: [],
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
import { getBillingCapabilityMessage } from "../src/pages/billingCapabilityMessages";

describe("getBillingCapabilityMessage", () => {
  it.each([
    ["PastDue", { status: "PastDue", capability: { checkoutAvailable: true, message: "Stripe billing is configured." } }, "Your payment is past due. Update your payment method to restore service."],
    ["CancelAtPeriodEnd", { status: "CancelAtPeriodEnd", capability: { checkoutAvailable: true, message: "Stripe billing is configured." } }, "Your subscription is scheduled to end at the close of the current billing period."],
    ["CancelAtPeriodEndFlag", { status: "Active", cancelAtPeriodEnd: true, capability: { checkoutAvailable: true, message: "Stripe billing is configured." } }, "Your subscription is scheduled to end at the close of the current billing period."],
    ["Cancelled", { status: "Cancelled", capability: { checkoutAvailable: true, message: "Stripe billing is configured." } }, "Your subscription has been cancelled. You can review billing history or start a new plan when you’re ready."],
    ["Trialing", { status: "Trialing", capability: { checkoutAvailable: true, message: "Stripe billing is configured." } }, "Your trial is active. Add a payment method before it ends to keep access."],
    ["Suspended", { status: "Suspended", capability: { checkoutAvailable: true, message: "Stripe billing is configured." } }, "Your subscription is suspended. Update billing details or contact support to restore access."]
  ])("returns the expected message for %s", (_label, subscription, expectedMessage) => {
    expect(getBillingCapabilityMessage(subscription as never)).toBe(expectedMessage);
  });
});

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
      hasStripeSubscription: false,
      capability: {
        provider: "Development",
        stripeConfigured: false,
        checkoutAvailable: false,
        portalAvailable: false,
        paymentMethodManagementAvailable: false,
        message: "Checkout is unavailable because Stripe billing is not fully configured in this environment.",
        missingConfiguration: ["Billing:Provider", "Billing:StripeSecretKey"]
      }
    };

    vi.mocked(useBillingDashboard).mockReturnValueOnce({
      subscription: freeTenantSubscription,
      paymentOptions: [],
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

  it("shows accepted payment methods when the host billing provider exposes them", () => {
    const hostPaymentOptions = [{
      key: "Mpesa",
      label: "M-Pesa",
      description: "Pay securely with M-Pesa mobile money."
    }];

    vi.mocked(useBillingDashboard).mockReturnValueOnce({
      subscription: {
        companyId: "c1",
        status: "Active",
        cancelAtPeriodEnd: false,
        currentPeriodStartUtc: "2026-08-01T00:00:00Z",
        currentPeriodEndUtc: "2026-09-01T00:00:00Z",
        trialEndsAtUtc: null,
        planName: "Starter",
        canStartCheckout: true,
        canOpenBillingPortal: true,
        canManagePaymentMethod: true,
        canCancel: true,
        canResume: false,
        hasStripeCustomer: true,
        hasStripeSubscription: true,
        capability: {
          provider: "Development",
          stripeConfigured: true,
          checkoutAvailable: true,
          portalAvailable: true,
          paymentMethodManagementAvailable: true,
          message: "M-Pesa is enabled for local Kenya transactions.",
          missingConfiguration: []
        }
      },
      paymentOptions: hostPaymentOptions,
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

    expect(screen.getByText("Accepted payment methods")).toBeInTheDocument();
    expect(screen.getByText("M-Pesa")).toBeInTheDocument();
    expect(screen.getByText("Pay securely with M-Pesa mobile money.")).toBeInTheDocument();
  });

  it("shows checkout capability message when Stripe billing is not configured", () => {
    const noStripeSubscription = {
      companyId: "c1",
      status: "Active",
      cancelAtPeriodEnd: false,
      currentPeriodStartUtc: "2026-08-01T00:00:00Z",
      currentPeriodEndUtc: "2026-09-01T00:00:00Z",
      trialEndsAtUtc: null,
      planName: "Free",
      canStartCheckout: false,
      canOpenBillingPortal: false,
      canManagePaymentMethod: false,
      canCancel: false,
      canResume: false,
      hasStripeCustomer: false,
      hasStripeSubscription: false,
      capability: {
        provider: "Development",
        stripeConfigured: false,
        checkoutAvailable: false,
        portalAvailable: false,
        paymentMethodManagementAvailable: false,
        message: "Checkout is unavailable because Stripe billing is not fully configured in this environment.",
        missingConfiguration: ["Billing:Provider", "Billing:StripeSecretKey", "Billing:PlanPriceIds:Starter"]
      }
    };

    vi.mocked(useBillingDashboard).mockReturnValueOnce({
      subscription: noStripeSubscription,
      paymentOptions: [],
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

    const capabilityMessage = /checkout is unavailable because stripe billing is not fully configured in this environment\./i;

    const currentSubscriptionCard = screen.getByRole("heading", { name: "Current Subscription" }).closest("article");
    const planComparisonCard = screen.getByRole("heading", { name: "Plan Comparison" }).closest("article");
    const usageSummaryCard = screen.getByRole("heading", { name: "Usage Summary" }).closest("article");
    const invoiceHistoryCard = screen.getByRole("heading", { name: "Invoice History" }).closest("article");

    expect(currentSubscriptionCard).not.toBeNull();
    expect(planComparisonCard).not.toBeNull();
    expect(usageSummaryCard).not.toBeNull();
    expect(invoiceHistoryCard).not.toBeNull();

    expect(within(currentSubscriptionCard!).getByText(capabilityMessage)).toBeInTheDocument();
    expect(within(planComparisonCard!).getByText(capabilityMessage)).toBeInTheDocument();
    expect(within(usageSummaryCard!).getByText(capabilityMessage)).toBeInTheDocument();
    expect(within(invoiceHistoryCard!).getByText(capabilityMessage)).toBeInTheDocument();
  });
});
