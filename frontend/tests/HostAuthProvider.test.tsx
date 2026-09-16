import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { StrictMode } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import App from "../src/App";

const hostTokenStorageKey = "stayflow.host.accessToken";
const hostRefreshTokenStorageKey = "stayflow.host.refreshToken";

const companyA = "11111111-1111-4111-8111-111111111111";
const companyB = "22222222-2222-4222-8222-222222222222";

function apiSuccess<T>(data: T) {
  return {
    ok: true,
    status: 200,
    json: async () => ({ success: true, message: "ok", data, errors: [], correlationId: "cid" })
  };
}

function apiFailure(message = "Request failed", status = 500) {
  return {
    ok: false,
    status,
    json: async () => ({ success: false, message, errors: [message], correlationId: "cid" })
  };
}

function profileFor(companyId: string) {
  return {
    id: "user-1",
    companyId,
    fullName: "Owner",
    email: "owner@stayflow.test",
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

function organizationsList(activeCompanyId: string) {
  return [
    {
      companyId: companyA,
      name: "Org A",
      slug: "org-a",
      role: "Owner",
      membershipStatus: "Active",
      isActiveOrganization: activeCompanyId === companyA,
      organizationStatus: "Active",
      onboardingState: "Completed",
      propertyCount: 1,
      planName: "Free",
      subscriptionStatus: "Active"
    },
    {
      companyId: companyB,
      name: "Org B",
      slug: "org-b",
      role: "Owner",
      membershipStatus: "Active",
      isActiveOrganization: activeCompanyId === companyB,
      organizationStatus: "Active",
      onboardingState: "Completed",
      propertyCount: 1,
      planName: "Free",
      subscriptionStatus: "Active"
    }
  ];
}

function onboardingStatusFor(companyId: string) {
  return {
    companyId,
    userId: "user-1",
    currentStep: "Completed",
    currentStepState: "Completed",
    completedSteps: [],
    remainingSteps: [],
    skippedSteps: [],
    blockers: [],
    checklist: [],
    percentComplete: 100,
    safeLinks: [{ rel: "host_inbox", href: "/host/conversations" }],
    startedAtUtc: "2026-08-01T00:00:00Z",
    isCompleted: true,
    lastUpdatedAtUtc: "2026-08-01T00:00:00Z",
    version: 1
  };
}

function emptyConversationsResponse() {
  return { items: [], totalCount: 0, page: 1, pageSize: 20, totalPages: 1, totalUnreadCount: 0 };
}

interface RouterOptions {
  activeCompanyId: string;
  /** Delays the /auth/organizations response to simulate out-of-order resolution. */
  organizationsDelayMs?: number;
  /** When true, /auth/me returns a profile with no companyId (unreconcilable state). */
  reportNoCompany?: boolean;
}

function createRouterFetchMock(options: RouterOptions) {
  const state = { activeCompanyId: options.activeCompanyId };
  const switchCalls: string[] = [];
  const meCallCount = { value: 0 };

  const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = typeof input === "string" ? input : input.toString();

    if (url.endsWith("/auth/me")) {
      meCallCount.value += 1;
      if (options.reportNoCompany) {
        return apiSuccess({ ...profileFor(""), companyId: "" });
      }
      return apiSuccess(profileFor(state.activeCompanyId));
    }

    if (url.endsWith("/auth/organizations")) {
      const respond = () => apiSuccess(organizationsList(state.activeCompanyId));
      if (options.organizationsDelayMs) {
        await new Promise((resolve) => setTimeout(resolve, options.organizationsDelayMs));
      }
      return respond();
    }

    if (url.endsWith("/auth/organizations/switch")) {
      const body = JSON.parse(String(init?.body ?? "{}")) as { companyId: string };
      switchCalls.push(body.companyId);
      state.activeCompanyId = body.companyId;
      return apiSuccess({
        accessToken: `${body.companyId}-access-token`,
        refreshToken: `${body.companyId}-refresh-token`,
        expiresAt: "2026-08-01T00:30:00Z"
      });
    }

    if (url.endsWith("/api/onboarding/status")) {
      return apiSuccess(onboardingStatusFor(state.activeCompanyId));
    }

    if (url.includes("/conversations?")) {
      return apiSuccess(emptyConversationsResponse());
    }

    return apiFailure(`Unhandled route ${url}`, 404);
  });

  return { fetchMock, switchCalls, meCallCount, state };
}

describe("HostAuthProvider shared state (two-organization incident regression)", () => {
  beforeEach(() => {
    vi.stubEnv("VITE_STAYFLOW_API_URL", "http://test.local");
    sessionStorage.setItem(hostTokenStorageKey, "token");
    sessionStorage.setItem(hostRefreshTokenStorageKey, "refresh");
  });

  it("never auto-switches merely because a second authorized organization exists", async () => {
    const { fetchMock, switchCalls } = createRouterFetchMock({ activeCompanyId: companyA });
    vi.stubGlobal("fetch", fetchMock);
    window.history.pushState({}, "", "/host/conversations");

    // OnboardingGate and HostInboxPage both mount here, both consuming useHostAuth().
    render(<App />);

    await waitFor(() => expect(screen.getByText("Org A")).toBeInTheDocument());

    expect(switchCalls).toEqual([]);
  });

  it("StrictMode double-invoked effects do not trigger an organization switch", async () => {
    const { fetchMock, switchCalls } = createRouterFetchMock({ activeCompanyId: companyA });
    vi.stubGlobal("fetch", fetchMock);
    window.history.pushState({}, "", "/host/conversations");

    render(
      <StrictMode>
        <App />
      </StrictMode>
    );

    await waitFor(() => expect(screen.getByText("Org A")).toBeInTheDocument());
    // Give any redundant StrictMode-doubled effect invocations a chance to settle.
    await new Promise((resolve) => setTimeout(resolve, 50));

    expect(switchCalls).toEqual([]);
  });

  it("delayed/out-of-order organization-list responses do not cause a switch", async () => {
    const { fetchMock, switchCalls } = createRouterFetchMock({ activeCompanyId: companyA, organizationsDelayMs: 100 });
    vi.stubGlobal("fetch", fetchMock);
    window.history.pushState({}, "", "/host/conversations");

    render(<App />);

    await waitFor(() => expect(screen.getByText("Org A")).toBeInTheDocument(), { timeout: 3000 });

    expect(switchCalls).toEqual([]);
  });

  it("fails safely without switching when organization state cannot be reconciled", async () => {
    const { fetchMock, switchCalls } = createRouterFetchMock({ activeCompanyId: companyA, reportNoCompany: true });
    vi.stubGlobal("fetch", fetchMock);
    window.history.pushState({}, "", "/host/conversations");

    render(<App />);

    await waitFor(() => expect(screen.getByText(/host sign in/i)).toBeInTheDocument());

    expect(switchCalls).toEqual([]);
  });
});

describe("HostAuthProvider explicit organization switch", () => {
  beforeEach(() => {
    vi.stubEnv("VITE_STAYFLOW_API_URL", "http://test.local");
    sessionStorage.setItem(hostTokenStorageKey, "token");
    sessionStorage.setItem(hostRefreshTokenStorageKey, "refresh");
  });

  it("switches exactly once, persists the replacement session, and every consumer observes the new company", async () => {
    const { fetchMock, switchCalls } = createRouterFetchMock({ activeCompanyId: companyA });
    vi.stubGlobal("fetch", fetchMock);
    window.history.pushState({}, "", "/host/conversations");
    const user = userEvent.setup();

    render(<App />);

    await waitFor(() => expect(screen.getByText("Org A")).toBeInTheDocument());

    await user.click(screen.getByRole("button", { expanded: false, name: /org a/i }));
    await user.click(await screen.findByRole("menuitem", { name: /org b/i }));

    await waitFor(() => expect(screen.getByText("Org B")).toBeInTheDocument());

    expect(switchCalls).toEqual([companyB]);
    expect(sessionStorage.getItem(hostTokenStorageKey)).toBe(`${companyB}-access-token`);
    expect(sessionStorage.getItem(hostRefreshTokenStorageKey)).toBe(`${companyB}-refresh-token`);

    // Subsequent authenticated calls (e.g. the onboarding status re-fetch after switching)
    // must use the replacement access token, not the original one.
    const onboardingCallsAfterSwitch = fetchMock.mock.calls.filter(([, init]) => {
      const headers = (init as RequestInit | undefined)?.headers as Record<string, string> | undefined;
      return headers?.Authorization === `Bearer ${companyB}-access-token`;
    });
    expect(onboardingCallsAfterSwitch.length).toBeGreaterThan(0);

    // No further automatic/fallback switch should occur once settled on the new company.
    expect(switchCalls).toEqual([companyB]);
  });
});
