import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { HostInboxPage } from "../src/pages/HostInboxPage";

const hostTokenStorageKey = "stayflow.host.accessToken";
const hostRefreshTokenStorageKey = "stayflow.host.refreshToken";

const organizationA = { companyId: "11111111-1111-4111-8111-111111111111", name: "Testing 1", isCompleted: false };
const organizationB = { companyId: "22222222-2222-4222-8222-222222222222", name: "Testing 2", isCompleted: true };

const banner = /onboarding is still in progress/i;

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

function profileFor(companyId: string) {
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

function organizationSummaries(activeCompanyId: string) {
  return [organizationA, organizationB].map((item) => ({
    companyId: item.companyId,
    name: item.name,
    slug: item.name.toLowerCase().replace(" ", "-"),
    role: "Owner",
    membershipStatus: "Active",
    isActiveOrganization: item.companyId === activeCompanyId,
    organizationStatus: "Active",
    onboardingState: item.isCompleted ? "Completed" : "FirstProperty",
    propertyCount: 1,
    planName: "Free",
    subscriptionStatus: "Active"
  }));
}

function onboardingStatus(companyId: string, isCompleted: boolean) {
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
    safeLinks: [],
    startedAtUtc: "2026-08-01T10:00:00Z",
    isCompleted,
    lastUpdatedAtUtc: "2026-08-01T10:00:00Z",
    version: 3
  };
}

function tokenFor(companyId: string) {
  return `token::${companyId}`;
}

function companyIdFromRequest(options?: RequestInit): string {
  const headers = (options?.headers ?? {}) as Record<string, string>;
  return (headers.Authorization ?? "").replace("Bearer token::", "");
}

interface FetchMockOptions {
  onboardingStatusOverride?: { companyId: string; isCompleted: boolean };
}

function createFetchMock(config?: FetchMockOptions) {
  return vi.fn().mockImplementation((url: string, options?: RequestInit) => {
    const activeCompanyId = companyIdFromRequest(options);

    if (url.endsWith("/auth/organizations/switch")) {
      const requested = JSON.parse(String(options?.body ?? "{}")) as { companyId: string };
      return Promise.resolve(
        apiSuccess({
          accessToken: tokenFor(requested.companyId),
          refreshToken: "refresh",
          expiresAt: "2026-08-12T12:00:00Z"
        })
      );
    }

    if (url.endsWith("/auth/organizations")) {
      return Promise.resolve(apiSuccess(organizationSummaries(activeCompanyId)));
    }

    if (url.endsWith("/auth/me")) {
      return Promise.resolve(apiSuccess(profileFor(activeCompanyId)));
    }

    if (url.endsWith("/api/onboarding/status")) {
      // The override simulates a response that belongs to a different organization.
      const override = config?.onboardingStatusOverride;
      return Promise.resolve(
        apiSuccess(override
          ? onboardingStatus(override.companyId, override.isCompleted)
          : onboardingStatus(activeCompanyId, activeCompanyId === organizationB.companyId))
      );
    }

    if (url.includes("/conversations")) {
      return Promise.resolve(
        apiSuccess({ items: [], totalCount: 0, page: 1, pageSize: 10, totalPages: 1, totalUnreadCount: 0 })
      );
    }

    return Promise.resolve(apiFailure(`Unhandled route ${url}`));
  });
}

async function switchToOrganization(user: ReturnType<typeof userEvent.setup>, name: string) {
  await user.click(screen.getByRole("button", { expanded: false, name: /testing/i }));
  await user.click(await screen.findByRole("menuitem", { name: new RegExp(name, "i") }));
}

describe("HostInboxPage onboarding banner", () => {
  beforeEach(() => {
    vi.stubEnv("VITE_STAYFLOW_API_URL", "http://test.local");
    sessionStorage.setItem(hostRefreshTokenStorageKey, "refresh");
  });

  it("hides the banner when the active organization completed onboarding", async () => {
    sessionStorage.setItem(hostTokenStorageKey, tokenFor(organizationB.companyId));
    vi.stubGlobal("fetch", createFetchMock());

    render(<HostInboxPage />);

    await waitFor(() => expect(screen.getByText(/testing 2/i)).toBeInTheDocument());
    expect(screen.queryByText(banner)).not.toBeInTheDocument();
  });

  it("shows the banner when the active organization has not completed onboarding", async () => {
    sessionStorage.setItem(hostTokenStorageKey, tokenFor(organizationA.companyId));
    vi.stubGlobal("fetch", createFetchMock());

    render(<HostInboxPage />);

    expect(await screen.findByText(banner)).toBeInTheDocument();
  });

  it("switching from an incomplete organization to a completed organization hides the banner", async () => {
    sessionStorage.setItem(hostTokenStorageKey, tokenFor(organizationA.companyId));
    vi.stubGlobal("fetch", createFetchMock());
    const user = userEvent.setup();

    render(<HostInboxPage />);

    expect(await screen.findByText(banner)).toBeInTheDocument();

    await switchToOrganization(user, organizationB.name);

    await waitFor(() => expect(screen.queryByText(banner)).not.toBeInTheDocument());
  });

  it("switching from a completed organization back to an incomplete organization shows the banner", async () => {
    sessionStorage.setItem(hostTokenStorageKey, tokenFor(organizationB.companyId));
    vi.stubGlobal("fetch", createFetchMock());
    const user = userEvent.setup();

    render(<HostInboxPage />);

    await waitFor(() => expect(screen.getByText(/testing 2/i)).toBeInTheDocument());
    expect(screen.queryByText(banner)).not.toBeInTheDocument();

    await switchToOrganization(user, organizationA.name);

    expect(await screen.findByText(banner)).toBeInTheDocument();
  });

  it("ignores an incomplete onboarding status belonging to another organization", async () => {
    sessionStorage.setItem(hostTokenStorageKey, tokenFor(organizationB.companyId));
    vi.stubGlobal("fetch", createFetchMock({
      onboardingStatusOverride: { companyId: organizationA.companyId, isCompleted: false }
    }));

    render(<HostInboxPage />);

    await waitFor(() => expect(screen.getByText(/testing 2/i)).toBeInTheDocument());
    expect(screen.queryByText(banner)).not.toBeInTheDocument();
  });

  it("ignores a completed onboarding status belonging to another organization", async () => {
    sessionStorage.setItem(hostTokenStorageKey, tokenFor(organizationA.companyId));
    vi.stubGlobal("fetch", createFetchMock({
      onboardingStatusOverride: { companyId: organizationB.companyId, isCompleted: true }
    }));

    render(<HostInboxPage />);

    await waitFor(() => expect(screen.getByText(/testing 1/i)).toBeInTheDocument());
    expect(screen.queryByText(banner)).not.toBeInTheDocument();
  });

  it("reloading the page keeps the banner state of the persisted organization", async () => {
    sessionStorage.setItem(hostTokenStorageKey, tokenFor(organizationA.companyId));
    vi.stubGlobal("fetch", createFetchMock());

    const first = render(<HostInboxPage />);
    expect(await screen.findByText(banner)).toBeInTheDocument();
    first.unmount();

    render(<HostInboxPage />);
    expect(await screen.findByText(banner)).toBeInTheDocument();
  });
});
