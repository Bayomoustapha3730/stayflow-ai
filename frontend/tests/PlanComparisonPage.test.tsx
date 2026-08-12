import { fireEvent, render, screen, waitFor } from "@testing-library/react";
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
        provider: "Stripe",
        stripeConfigured: true,
        checkoutAvailable: true,
        portalAvailable: true,
        paymentMethodManagementAvailable: true,
        message: "Stripe billing is configured.",
        missingConfiguration: []
      }
    },
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
  })
}));

import { PlanComparisonPage } from "../src/pages/PlanComparisonPage";

describe("PlanComparisonPage", () => {
  it("opens upgrade dialog and confirms plan change", async () => {
    render(<PlanComparisonPage />);

    fireEvent.click(screen.getAllByRole("button", { name: "Upgrade Plan" })[0]);

    expect(screen.getByRole("heading", { name: "Confirm Upgrade" })).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "Confirm Upgrade" }));

    await waitFor(() => {
      expect(changePlan).toHaveBeenCalled();
    });
  });
});
