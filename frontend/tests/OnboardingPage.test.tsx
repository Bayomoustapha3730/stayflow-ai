import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import type { OnboardingStatus } from "../src/models/onboarding";

const start = vi.fn().mockResolvedValue({
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
  safeLinks: [{ rel: "current_step", href: "/onboarding/organization" }],
  startedAtUtc: "2026-08-01T00:00:00Z",
  selectedPlanName: null,
  firstPropertyId: null,
  isCompleted: false,
  completedAtUtc: null,
  completedByUserId: null,
  lastUpdatedAtUtc: "2026-08-01T00:00:00Z",
  version: 1
});

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

const onboardingState: { status: OnboardingStatus; error: string | null; message: string | null } & Record<string, unknown> = {
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
    reviewSummary: {
      organizationName: "StayFlow KE",
      organizationSlug: "stayflow-ke",
      organizationSupportEmail: "support@stayflow.test",
      organizationTimeZone: "Africa/Nairobi",
      selectedPlanName: "Growth",
      firstPropertyId: "p1",
      firstPropertyName: "Nairobi Loft",
      teamInvitationsState: "Completed",
      teamInvitations: [{ email: "host1@test.io", role: "Host", status: "Pending" }],
      whatsAppSetupState: "Skipped",
      whatsAppIntegrationName: "Demo WhatsApp Concierge",
      aiProviderState: "Completed",
      aiProvider: "Development",
      knowledgeSetupState: "Completed",
      knowledgeTitle: "House Rules",
      demoDataState: "Skipped"
    },
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
  message: null,  refresh: vi.fn(),
  start,
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
};

vi.mock("../src/hooks/useHostAuth", () => ({
  useHostAuth: () => hostAuthState
}));

vi.mock("../src/hooks/useOnboardingWizard", () => ({
  useOnboardingWizard: () => onboardingState
}));

import { OnboardingPage } from "../src/pages/OnboardingPage";

describe("OnboardingPage", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    onboardingState.status.currentStep = "OrganizationProfile";
    onboardingState.status.completedSteps = ["Welcome"];
    onboardingState.status.skippedSteps = [];
    onboardingState.status.isCompleted = false;
    onboardingState.error = null;
    window.history.pushState({}, "", "/onboarding/organization");
  });

  it("starts onboarding and navigates to backend canonical current step", async () => {
    window.history.pushState({}, "", "/onboarding/welcome");
    onboardingState.status.currentStep = "Welcome";
    onboardingState.status.completedSteps = [];

    render(<OnboardingPage routeStep="welcome" />);

    fireEvent.click(screen.getByRole("button", { name: "Start Onboarding" }));

    expect(start).toHaveBeenCalledTimes(1);
    await waitFor(() => {
      expect(window.location.pathname).toBe("/onboarding/organization");
    });
  });

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

  it("does not show a stale demo error or demo actions after skipped onboarding is completed", () => {
    onboardingState.status.currentStep = "Completed";
    onboardingState.status.completedSteps = ["Welcome", "Completed"];
    onboardingState.status.skippedSteps = ["DemoData"];
    onboardingState.status.isCompleted = true;
    onboardingState.error = "Demo data step is not available yet.";

    render(<OnboardingPage routeStep="demo" />);

    expect(screen.getByText("You're Ready")).toBeInTheDocument();
    expect(screen.queryByRole("alert")).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Generate Demo Data" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Skip" })).not.toBeInTheDocument();
  });

  it("keeps demo actions available before demo data is generated or skipped", () => {
    onboardingState.status.currentStep = "DemoData";
    window.history.pushState({}, "", "/onboarding/demo");

    render(<OnboardingPage routeStep="demo" />);

    expect(screen.getByRole("button", { name: "Generate Demo Data" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Skip" })).toBeInTheDocument();
  });

  it("renders review summary with persisted onboarding inputs", () => {
    render(<OnboardingPage routeStep="review" />);

    expect(screen.getByText(/StayFlow KE/)).toBeInTheDocument();
    expect(screen.getByText(/Growth/)).toBeInTheDocument();
    expect(screen.getByText(/Nairobi Loft/)).toBeInTheDocument();
    expect(screen.getByText(/Demo data choice/i)).toBeInTheDocument();
  });

  it("shows Free in plan confirmation when no paid plan is selected", () => {
    onboardingState.status.currentStep = "PlanConfirmation";
    onboardingState.status.selectedPlanName = null;

    render(<OnboardingPage routeStep="plan" />);

    expect(screen.getByText(/Current plan:/i)).toBeInTheDocument();
    expect(screen.getByText("Free")).toBeInTheDocument();
    expect(screen.queryByText("Unknown")).not.toBeInTheDocument();
  });
});
