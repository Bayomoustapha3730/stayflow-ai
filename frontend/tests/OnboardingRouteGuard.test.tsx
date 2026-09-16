import { render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

vi.mock("../src/pages/HostInboxPage", () => ({
  HostInboxPage: () => <div data-testid="host-inbox-page">host-inbox</div>
}));

vi.mock("../src/pages/OnboardingPage", () => ({
  OnboardingPage: ({ routeStep }: { routeStep?: string }) => <div data-testid="onboarding-page">{routeStep ?? "root"}</div>
}));

import App from "../src/App";

const hostTokenStorageKey = "stayflow.host.accessToken";
const hostRefreshTokenStorageKey = "stayflow.host.refreshToken";
const companyId = "11111111-1111-4111-8111-111111111111";

function apiSuccess<T>(data: T) {
  return {
    ok: true,
    status: 200,
    headers: { get: () => null },
    json: async () => ({ success: true, message: "ok", data, errors: [], correlationId: "cid" })
  };
}

function apiFailure(message = "Request failed", status = 500) {
  return {
    ok: false,
    status,
    headers: { get: () => null },
    json: async () => ({ success: false, message, errors: [message], correlationId: "cid" })
  };
}

function profile() {
  return {
    id: "user-1",
    companyId,
    fullName: "Owner",
    email: "owner@test.local",
    phoneNumber: "+254700000001",
    preferredLanguage: "en",
    timeZone: "Africa/Nairobi",
    isEmailVerified: true,
    emailNotificationsEnabled: true,
    securityNotificationsEnabled: true,
    organizationRole: "Owner",
    permissions: []
  };
}

function organizationSummaries() {
  return [{
    companyId,
    name: "StayFlow KE",
    slug: "stayflow-ke",
    role: "Owner",
    membershipStatus: "Active",
    isActiveOrganization: true,
    organizationStatus: "Active",
    onboardingState: "OrganizationProfile",
    propertyCount: 0,
    planName: "Free",
    subscriptionStatus: "Active"
  }];
}

function onboardingStatus(isCompleted: boolean) {
  return {
    companyId,
    userId: "user-1",
    currentStep: isCompleted ? "Completed" : "FirstProperty",
    currentStepState: isCompleted ? "Completed" : "InProgress",
    completedSteps: [],
    remainingSteps: [],
    skippedSteps: [],
    blockers: [],
    checklist: [],
    percentComplete: isCompleted ? 100 : 40,
    safeLinks: isCompleted
      ? [{ rel: "host_inbox", href: "/host/conversations" }]
      : [{ rel: "current_step", href: "/onboarding/property" }],
    startedAtUtc: "2026-08-01T10:00:00Z",
    isCompleted,
    lastUpdatedAtUtc: "2026-08-01T10:00:00Z",
    version: 3
  };
}

type OnboardingStatusMode = "completed" | "incomplete" | "failure" | "pending";

function createFetchMock(mode: OnboardingStatusMode) {
  let releasePending: (() => void) | undefined;
  const pendingGate = new Promise<void>((resolve) => {
    releasePending = resolve;
  });

  const mock = vi.fn().mockImplementation(async (url: string) => {
    if (url.endsWith("/auth/organizations")) {
      return apiSuccess(organizationSummaries());
    }

    if (url.endsWith("/auth/me")) {
      return apiSuccess(profile());
    }

    if (url.endsWith("/api/onboarding/status")) {
      if (mode === "failure") {
        return apiFailure("Onboarding status unavailable", 500);
      }

      if (mode === "pending") {
        await pendingGate;
        return apiSuccess(onboardingStatus(false));
      }

      return apiSuccess(onboardingStatus(mode === "completed"));
    }

    return apiFailure(`Unhandled route ${url}`);
  });

  return { fetchMock: mock, releasePending: () => releasePending?.() };
}

describe("Onboarding route guard", () => {
  beforeEach(() => {
    vi.stubEnv("VITE_STAYFLOW_API_URL", "http://test.local");
    sessionStorage.setItem(hostTokenStorageKey, "token");
    sessionStorage.setItem(hostRefreshTokenStorageKey, "refresh");
  });

  it("redirects a protected route to onboarding when onboarding is incomplete", async () => {
    const { fetchMock } = createFetchMock("incomplete");
    vi.stubGlobal("fetch", fetchMock);
    window.history.pushState({}, "", "/host/conversations");

    render(<App />);

    await waitFor(() => expect(window.location.pathname).toBe("/onboarding/property"));
    expect(await screen.findByTestId("onboarding-page")).toBeInTheDocument();
    expect(screen.queryByTestId("host-inbox-page")).not.toBeInTheDocument();
  });

  it("allows a protected route when onboarding is completed", async () => {
    const { fetchMock } = createFetchMock("completed");
    vi.stubGlobal("fetch", fetchMock);
    window.history.pushState({}, "", "/host/conversations");

    render(<App />);

    expect(await screen.findByTestId("host-inbox-page")).toBeInTheDocument();
    expect(window.location.pathname).toBe("/host/conversations");
  });

  it("redirects away from onboarding routes when onboarding is already completed", async () => {
    const { fetchMock } = createFetchMock("completed");
    vi.stubGlobal("fetch", fetchMock);
    window.history.pushState({}, "", "/onboarding");

    render(<App />);

    await waitFor(() => expect(window.location.pathname).toBe("/host/conversations"));
    expect(await screen.findByTestId("host-inbox-page")).toBeInTheDocument();
  });

  it("does not render the protected page while onboarding status is loading", async () => {
    const { fetchMock, releasePending } = createFetchMock("pending");
    vi.stubGlobal("fetch", fetchMock);
    window.history.pushState({}, "", "/host/conversations");

    render(<App />);

    expect(screen.queryByTestId("host-inbox-page")).not.toBeInTheDocument();
    expect(screen.getByText("Loading...")).toBeInTheDocument();

    releasePending();

    await waitFor(() => expect(window.location.pathname).toBe("/onboarding/property"));
  });

  it("does not allow the protected route when the onboarding status request fails", async () => {
    const { fetchMock } = createFetchMock("failure");
    vi.stubGlobal("fetch", fetchMock);
    window.history.pushState({}, "", "/host/conversations");

    render(<App />);

    await screen.findByRole("alert");
    expect(screen.queryByTestId("host-inbox-page")).not.toBeInTheDocument();
    expect(window.location.pathname).toBe("/host/conversations");
  });

  it("leaves public routes unaffected regardless of onboarding status", async () => {
    const { fetchMock } = createFetchMock("incomplete");
    vi.stubGlobal("fetch", fetchMock);
    window.history.pushState({}, "", "/privacy");

    render(<App />);

    expect(await screen.findByRole("heading", { level: 1, name: /privacy policy/i })).toBeInTheDocument();
    expect(window.location.pathname).toBe("/privacy");
  });
});
