import { act, renderHook, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { useHostAuth } from "../src/hooks/useHostAuth";

function loginSuccessResponse() {
  return {
    ok: true,
    status: 200,
    json: async () => ({
      success: true,
      message: "ok",
      data: {
        accessToken: "host-access-token",
        refreshToken: "refresh-token",
        expiresAt: "2026-07-22T12:00:00Z"
      },
      errors: [],
      correlationId: "cid"
    })
  };
}

function currentUserSuccessResponse() {
  return {
    ok: true,
    status: 200,
    json: async () => ({
      success: true,
      message: "ok",
      data: {
        id: "user-1",
        companyId: "company-1",
        fullName: "Host User",
        email: "host@example.com",
        phoneNumber: "+254700000000",
          preferredLanguage: "en",
          timeZone: "UTC",
        isEmailVerified: true,
          emailNotificationsEnabled: true,
          securityNotificationsEnabled: true,
          productUpdatesEnabled: false,
        organizationRole: "Administrator",
        roles: ["Host"],
        permissions: ["conversations.read"]
      },
      errors: [],
      correlationId: "cid"
    })
  };
}

function organizationsSuccessResponse(activeCompanyId = "company-1") {
  return {
    ok: true,
    status: 200,
    json: async () => ({
      success: true,
      message: "ok",
      data: [
        {
          companyId: activeCompanyId,
          name: activeCompanyId === "company-2" ? "Orbit Ops" : "StayFlow KE",
          slug: activeCompanyId === "company-2" ? "orbit-ops" : "stayflow-ke",
          role: "Administrator",
          membershipStatus: "Active",
          isActiveOrganization: true,
          organizationStatus: "Active",
          onboardingState: "Completed",
          propertyCount: 1,
          planName: "Free",
          subscriptionStatus: "Active"
        },
        {
          companyId: activeCompanyId === "company-2" ? "company-1" : "company-2",
          name: activeCompanyId === "company-2" ? "StayFlow KE" : "Orbit Ops",
          slug: activeCompanyId === "company-2" ? "stayflow-ke" : "orbit-ops",
          role: "Owner",
          membershipStatus: "Active",
          isActiveOrganization: false,
          organizationStatus: "Active",
          onboardingState: "Completed",
          propertyCount: 2,
          planName: "Growth",
          subscriptionStatus: "Active"
        }
      ],
      errors: [],
      correlationId: "cid"
    })
  };
}

function switchSuccessResponse() {
  return {
    ok: true,
    status: 200,
    json: async () => ({
      success: true,
      message: "ok",
      data: {
        accessToken: "org-2-access-token",
        refreshToken: "org-2-refresh-token",
        expiresAt: "2026-07-22T12:30:00Z"
      },
      errors: [],
      correlationId: "cid"
    })
  };
}

function onboardingCompletedResponse() {
  return {
    ok: true,
    status: 200,
    json: async () => ({
      success: true,
      message: "ok",
      data: {
        companyId: "company-2",
        userId: "user-1",
        currentStep: "Completed",
        currentStepState: "Completed",
        completedSteps: [],
        remainingSteps: [],
        skippedSteps: [],
        blockers: [],
        checklist: [],
        percentComplete: 100,
        nextRecommendedAction: null,
        safeLinks: [],
        startedAtUtc: "2026-08-01T00:00:00Z",
        selectedPlanName: "Free",
        firstPropertyId: null,
        isCompleted: true,
        completedAtUtc: "2026-08-02T00:00:00Z",
        completedByUserId: "user-1",
        lastUpdatedAtUtc: "2026-08-02T00:00:00Z",
        version: 1
      },
      errors: [],
      correlationId: "cid"
    })
  };
}

function loginFailureResponse() {
  return {
    ok: false,
    status: 401,
    json: async () => ({
      success: false,
      message: "Invalid credentials",
      errors: ["Invalid credentials"],
      correlationId: "cid"
    })
  };
}

describe("useHostAuth", () => {
  beforeEach(() => {
    sessionStorage.clear();
    vi.stubEnv("VITE_STAYFLOW_API_URL", "http://test.local");
  });

  it("stores host access token after successful login", async () => {
    vi.stubGlobal("fetch", vi
      .fn()
      .mockResolvedValueOnce(loginSuccessResponse())
      .mockResolvedValueOnce(currentUserSuccessResponse())
      .mockResolvedValueOnce(organizationsSuccessResponse())
      .mockResolvedValueOnce(currentUserSuccessResponse())
      .mockResolvedValueOnce(organizationsSuccessResponse()));

    const { result } = renderHook(() => useHostAuth());

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

    const { result } = renderHook(() => useHostAuth());

    await act(async () => {
      await expect(result.current.login("host@example.com", "wrong")).rejects.toThrow();
    });

    expect(result.current.error).toMatch(/session has expired|invalid credentials/i);
    expect(result.current.isAuthenticated).toBe(false);
  });

  it("logout clears token and auth state", async () => {
    vi.stubGlobal("fetch", vi
      .fn()
      .mockResolvedValueOnce(loginSuccessResponse())
      .mockResolvedValueOnce(currentUserSuccessResponse())
      .mockResolvedValueOnce(organizationsSuccessResponse())
      .mockResolvedValueOnce(currentUserSuccessResponse())
      .mockResolvedValueOnce(organizationsSuccessResponse()));

    const { result } = renderHook(() => useHostAuth());

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
    vi.stubGlobal("fetch", vi
      .fn()
      .mockResolvedValueOnce(loginSuccessResponse())
      .mockResolvedValueOnce(currentUserSuccessResponse())
      .mockResolvedValueOnce(organizationsSuccessResponse())
      .mockResolvedValueOnce(currentUserSuccessResponse())
      .mockResolvedValueOnce(organizationsSuccessResponse())
      .mockResolvedValueOnce(switchSuccessResponse())
      .mockResolvedValueOnce({
        ...currentUserSuccessResponse(),
        json: async () => ({
          success: true,
          message: "ok",
          data: {
            id: "user-1",
            companyId: "company-2",
            fullName: "Host User",
            email: "host@example.com",
            phoneNumber: "+254700000000",
            preferredLanguage: "en",
            timeZone: "UTC",
            isEmailVerified: true,
            emailNotificationsEnabled: true,
            securityNotificationsEnabled: true,
            productUpdatesEnabled: false,
            organizationRole: "Owner",
            roles: ["Host"],
            permissions: ["conversations.read"]
          },
          errors: [],
          correlationId: "cid"
        })
      })
      .mockResolvedValueOnce(organizationsSuccessResponse("company-2"))
      .mockResolvedValueOnce(onboardingCompletedResponse())
      .mockResolvedValueOnce({
        ...currentUserSuccessResponse(),
        json: async () => ({
          success: true,
          message: "ok",
          data: {
            id: "user-1",
            companyId: "company-2",
            fullName: "Host User",
            email: "host@example.com",
            phoneNumber: "+254700000000",
            preferredLanguage: "en",
            timeZone: "UTC",
            isEmailVerified: true,
            emailNotificationsEnabled: true,
            securityNotificationsEnabled: true,
            productUpdatesEnabled: false,
            organizationRole: "Owner",
            roles: ["Host"],
            permissions: ["conversations.read"]
          },
          errors: [],
          correlationId: "cid"
        })
      })
      .mockResolvedValueOnce(organizationsSuccessResponse("company-2")));

    const { result } = renderHook(() => useHostAuth());

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

    expect(sessionStorage.getItem("stayflow.host.accessToken")).toBe("org-2-access-token");
    expect(sessionStorage.getItem("stayflow.host.refreshToken")).toBe("org-2-refresh-token");
  });
});
