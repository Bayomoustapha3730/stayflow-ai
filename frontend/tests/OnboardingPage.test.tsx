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
    isEmailVerified: true,
    organizationRole: "Owner",
    roles: ["Owner"],
    permissions: []
  },
  isAuthenticated: true,
  isSigningIn: false,
  error: null,
  login: vi.fn(),
  logout: vi.fn(),
  clearError: vi.fn()
};

const saveOrganization = vi.fn().mockResolvedValue(undefined);

vi.mock("../src/hooks/useHostAuth", () => ({
  useHostAuth: () => hostAuthState
}));

vi.mock("../src/hooks/useOnboardingWizard", () => ({
  useOnboardingWizard: () => ({
    status: {
      companyId: "c1",
      userId: "u1",
      currentStep: "OrganizationProfile",
      currentStepState: "InProgress",
      completedSteps: ["Welcome"],
      remainingSteps: ["OrganizationProfile"],
      skippedSteps: [],
      blockers: [],
      checklist: [],
      percentComplete: 10,
      nextRecommendedAction: "Complete OrganizationProfile",
      safeLinks: [],
      startedAtUtc: "2026-08-01T00:00:00Z",
      selectedPlanName: null,
      firstPropertyId: null,
      isCompleted: false,
      completedAtUtc: null,
      completedByUserId: null,
      lastUpdatedAtUtc: "2026-08-01T00:00:00Z",
      version: 1
    },
    isLoading: false,
    isSaving: false,
    error: null,
    message: null,
    refresh: vi.fn(),
    start: vi.fn(),
    saveOrganization,
    confirmPlan: vi.fn(),
    createProperty: vi.fn(),
    submitInvitations: vi.fn(),
    configureWhatsApp: vi.fn(),
    configureAi: vi.fn(),
    submitKnowledge: vi.fn(),
    generateDemoData: vi.fn(),
    skipStep: vi.fn(),
    complete: vi.fn()
  })
}));

import { OnboardingPage } from "../src/pages/OnboardingPage";

describe("OnboardingPage", () => {
  it("renders organization step and submits save", () => {
    render(<OnboardingPage routeStep="organization" />);

    fireEvent.change(screen.getByLabelText("Name"), { target: { value: "StayFlow KE" } });
    fireEvent.click(screen.getByRole("button", { name: "Save and Continue" }));

    expect(saveOrganization).toHaveBeenCalled();
  });

  it("renders completion links for completed route", () => {
    render(<OnboardingPage routeStep="completed" />);

    expect(screen.getByText("You're Ready")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Go to Host Inbox" })).toBeInTheDocument();
  });
});
