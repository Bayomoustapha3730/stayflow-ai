import { act, renderHook, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { useOnboardingWizard } from "../src/hooks/useOnboardingWizard";

function ok(data: unknown) {
  return {
    ok: true,
    status: 200,
    headers: new Headers(),
    json: async () => ({
      success: true,
      message: "ok",
      data,
      errors: [],
      correlationId: "cid"
    })
  };
}

describe("useOnboardingWizard", () => {
  beforeEach(() => {
    vi.stubEnv("VITE_STAYFLOW_API_URL", "http://test.local");
  });

  it("updates local status immediately from start response", async () => {
    vi.stubGlobal("fetch", vi
      .fn()
      .mockResolvedValueOnce(ok({
        companyId: "c1",
        userId: "u1",
        currentStep: "Welcome",
        currentStepState: "InProgress",
        completedSteps: [],
        remainingSteps: ["Welcome", "OrganizationProfile"],
        skippedSteps: [],
        blockers: [],
        checklist: [],
        percentComplete: 0,
        nextRecommendedAction: "Start onboarding",
        safeLinks: [],
        startedAtUtc: "2026-08-01T00:00:00Z",
        selectedPlanName: null,
        firstPropertyId: null,
        isCompleted: false,
        completedAtUtc: null,
        completedByUserId: null,
        lastUpdatedAtUtc: "2026-08-01T00:00:00Z",
        version: 1
      }))
      .mockResolvedValueOnce(ok({
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
        lastUpdatedAtUtc: "2026-08-01T00:00:10Z",
        version: 2
      })));

    const { result } = renderHook(() => useOnboardingWizard({ accessToken: "token" }));

    await waitFor(() => {
      expect(result.current.status?.currentStep).toBe("Welcome");
    });

    await act(async () => {
      await result.current.start();
    });

    await waitFor(() => {
      expect(result.current.status?.currentStep).toBe("OrganizationProfile");
      expect(result.current.message).toBe("Onboarding started.");
    });

    expect(fetch).toHaveBeenCalledTimes(2);
  });

  it("does not auto-start onboarding when status request fails", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue({
      ok: false,
      status: 500,
      headers: new Headers(),
      json: async () => ({
        success: false,
        message: "failed",
        errors: ["failed"],
        correlationId: "cid"
      })
    }));

    const { result } = renderHook(() => useOnboardingWizard({ accessToken: "token" }));

    await waitFor(() => {
      expect(result.current.error).toBeTruthy();
    });

    expect(fetch).toHaveBeenCalledTimes(1);
  });
});
