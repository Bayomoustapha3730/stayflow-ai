import { act, renderHook, waitFor } from "@testing-library/react";
import type { ReactNode } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { useHostAuth } from "../src/hooks/useHostAuth";
import { HostAuthProvider } from "../src/providers/HostAuthProvider";

function ok<T>(data: T) {
  return {
    ok: true,
    status: 200,
    json: async () => ({ success: true, message: "ok", data, errors: [], correlationId: "cid" })
  };
}

function fail(message: string, status = 400) {
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
    fullName: "Host User",
    email: "host@example.com",
    phoneNumber: "+254700000000",
    preferredLanguage: "en",
    timeZone: "UTC",
    isEmailVerified: true,
    emailNotificationsEnabled: true,
    securityNotificationsEnabled: true,
    productUpdatesEnabled: false,
    organizationRole: companyId === "company-2" ? "Owner" : "Administrator",
    roles: ["Host"],
    permissions: ["conversations.read"]
  };
}

function organizationsFor(activeCompanyId: string) {
  return [
    {
      companyId: "company-1",
      name: "StayFlow KE",
      slug: "stayflow-ke",
      role: "Administrator",
      membershipStatus: "Active",
      isActiveOrganization: activeCompanyId === "company-1",
      organizationStatus: "Active",
      onboardingState: "Completed",
      propertyCount: 1,
      planName: "Free",
      subscriptionStatus: "Active"
    },
    {
      companyId: "company-2",
      name: "Orbit Ops",
      slug: "orbit-ops",
      role: "Owner",
      membershipStatus: "Active",
      isActiveOrganization: activeCompanyId === "company-2",
      organizationStatus: "Active",
      onboardingState: "Completed",
      propertyCount: 2,
      planName: "Growth",
      subscriptionStatus: "Active"
    }
  ];
}

interface RouterState {
  activeCompanyId: string;
}

function createRouterFetchMock(state: RouterState) {
  const switchCalls: string[] = [];

  const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = typeof input === "string" ? input : input.toString();

    if (url.endsWith("/auth/login")) {
      return ok({ accessToken: "host-access-token", refreshToken: "host-refresh-token", expiresAt: "2026-07-22T12:00:00Z" });
    }

    if (url.endsWith("/auth/me")) {
      return ok(profileFor(state.activeCompanyId));
    }

    if (url.endsWith("/auth/organizations")) {
      return ok(organizationsFor(state.activeCompanyId));
    }

    if (url.endsWith("/auth/organizations/switch")) {
      const body = JSON.parse(String(init?.body ?? "{}")) as { companyId: string };
      switchCalls.push(body.companyId);
      state.activeCompanyId = body.companyId;
      return ok({ accessToken: `${body.companyId}-access-token`, refreshToken: `${body.companyId}-refresh-token`, expiresAt: "2026-07-22T12:30:00Z" });
    }

    if (url.endsWith("/api/onboarding/status")) {
      return ok({
        companyId: state.activeCompanyId,
        userId: "user-1",
        currentStep: "Completed",
        currentStepState: "Completed",
        completedSteps: [],
        remainingSteps: [],
        skippedSteps: [],
        blockers: [],
        checklist: [],
        percentComplete: 100,
        safeLinks: [],
        startedAtUtc: "2026-08-01T00:00:00Z",
        isCompleted: true,
        lastUpdatedAtUtc: "2026-08-01T00:00:00Z",
        version: 1
      });
    }

    return fail(`Unhandled route ${url}`, 404);
  });

  return { fetchMock, switchCalls };
}

function loginFailureResponse() {
  return fail("Invalid credentials", 401);
}

function providerWrapper({ children }: { children: ReactNode }) {
  return <HostAuthProvider>{children}</HostAuthProvider>;
}

describe("useHostAuth", () => {
  beforeEach(() => {
    sessionStorage.clear();
    vi.stubEnv("VITE_STAYFLOW_API_URL", "http://test.local");
  });

  it("stores host access token after successful login", async () => {
    const { fetchMock } = createRouterFetchMock({ activeCompanyId: "company-1" });
    vi.stubGlobal("fetch", fetchMock);

    const { result } = renderHook(() => useHostAuth(), { wrapper: providerWrapper });

    await act(async () => {
      await result.current.login("host@example.com", "Password123!");
    });

    await waitFor(() => {
      expect(result.current.currentUser?.organizationRole).toBe("Administrator");
    });

    expect(result.current.isAuthenticated).toBe(true);
    expect(result.current.accessToken).toBe("host-access-token");
    expect(sessionStorage.getItem("stayflow.host.accessToken")).toBe("host-access-token");
  });

  it("exposes an error when login fails", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(loginFailureResponse()));

    const { result } = renderHook(() => useHostAuth(), { wrapper: providerWrapper });

    await act(async () => {
      await expect(result.current.login("host@example.com", "wrong")).rejects.toThrow();
    });

    expect(result.current.error).toMatch(/session has expired|invalid credentials/i);
    expect(result.current.isAuthenticated).toBe(false);
  });

  it("logout clears token and auth state", async () => {
    const { fetchMock } = createRouterFetchMock({ activeCompanyId: "company-1" });
    vi.stubGlobal("fetch", fetchMock);

    const { result } = renderHook(() => useHostAuth(), { wrapper: providerWrapper });

    await act(async () => {
      await result.current.login("host@example.com", "Password123!");
    });

    act(() => {
      result.current.logout();
    });

    expect(result.current.isAuthenticated).toBe(false);
    expect(result.current.currentUser).toBeNull();
    expect(sessionStorage.getItem("stayflow.host.accessToken")).toBeNull();
  });

  it("switches organizations using the backend token response and updates current user context", async () => {
    const { fetchMock, switchCalls } = createRouterFetchMock({ activeCompanyId: "company-1" });
    vi.stubGlobal("fetch", fetchMock);

    const { result } = renderHook(() => useHostAuth(), { wrapper: providerWrapper });

    await act(async () => {
      await result.current.login("host@example.com", "Password123!");
    });

    await act(async () => {
      const changed = await result.current.switchOrganization("company-2");
      expect(changed).toBe(true);
    });

    await waitFor(() => {
      expect(result.current.currentUser?.companyId).toBe("company-2");
    });

    expect(switchCalls).toEqual(["company-2"]);
    expect(sessionStorage.getItem("stayflow.host.accessToken")).toBe("company-2-access-token");
    expect(sessionStorage.getItem("stayflow.host.refreshToken")).toBe("company-2-refresh-token");
  });

  it("never calls the switch endpoint automatically merely from loading the current user", async () => {
    const { fetchMock, switchCalls } = createRouterFetchMock({ activeCompanyId: "company-1" });
    vi.stubGlobal("fetch", fetchMock);

    const { result } = renderHook(() => useHostAuth(), { wrapper: providerWrapper });

    await act(async () => {
      await result.current.login("host@example.com", "Password123!");
    });

    await waitFor(() => {
      expect(result.current.currentUser?.companyId).toBe("company-1");
    });

    expect(switchCalls).toEqual([]);
  });
});
