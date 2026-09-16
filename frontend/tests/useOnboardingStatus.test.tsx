import { act, renderHook, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { useOnboardingStatus } from "../src/hooks/useOnboardingStatus";

function ok(data: unknown) {
  return {
    ok: true,
    status: 200,
    headers: new Headers(),
    json: async () => ({ success: true, message: "ok", data, errors: [], correlationId: "cid" })
  };
}

function baseStatus(companyId: string, isCompleted = false) {
  return {
    companyId,
    userId: "user-1",
    currentStep: "FirstProperty",
    currentStepState: "InProgress",
    completedSteps: [],
    remainingSteps: [],
    skippedSteps: [],
    blockers: [],
    checklist: [],
    percentComplete: 40,
    safeLinks: [],
    startedAtUtc: "2026-08-01T00:00:00Z",
    isCompleted,
    lastUpdatedAtUtc: "2026-08-01T00:00:00Z",
    version: 1
  };
}

describe("useOnboardingStatus (canonical onboarding status hook)", () => {
  beforeEach(() => {
    vi.stubEnv("VITE_STAYFLOW_API_URL", "http://test.local");
  });

  it("fetches and resolves status when the company gate is satisfied", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(ok(baseStatus("c1"))));

    const { result } = renderHook(() => useOnboardingStatus({ accessToken: "token", activeCompanyId: "c1" }));

    await waitFor(() => expect(result.current.isResolved).toBe(true));
    expect(result.current.status?.companyId).toBe("c1");
    expect(result.current.error).toBeNull();
  });

  it("discards a response belonging to a different organization", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(ok(baseStatus("other-company"))));

    const { result } = renderHook(() => useOnboardingStatus({ accessToken: "token", activeCompanyId: "c1" }));

    await waitFor(() => expect(result.current.isResolved).toBe(true));
    expect(result.current.status).toBeNull();
  });

  it("fetches without a company gate when activeCompanyId is not provided", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(ok(baseStatus("any-company"))));

    const { result } = renderHook(() => useOnboardingStatus({ accessToken: "token" }));

    await waitFor(() => expect(result.current.status?.companyId).toBe("any-company"));
  });

  it("surfaces a request failure without marking onboarding complete", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue({
      ok: false,
      status: 500,
      headers: new Headers(),
      json: async () => ({ success: false, message: "failed", errors: ["failed"], correlationId: "cid" })
    }));

    const { result } = renderHook(() => useOnboardingStatus({ accessToken: "token", activeCompanyId: "c1" }));

    await waitFor(() => expect(result.current.error).toBeTruthy());
    expect(result.current.status).toBeNull();
    expect(result.current.isIncomplete).toBe(false);
  });

  it("applies a mutation-provided status without an extra request", async () => {
    const fetchMock = vi.fn().mockResolvedValue(ok(baseStatus("c1")));
    vi.stubGlobal("fetch", fetchMock);

    const { result } = renderHook(() => useOnboardingStatus({ accessToken: "token", activeCompanyId: "c1" }));
    await waitFor(() => expect(result.current.isResolved).toBe(true));

    const callsBefore = fetchMock.mock.calls.length;
    act(() => {
      result.current.applyStatus(baseStatus("c1", true));
    });

    expect(result.current.status?.isCompleted).toBe(true);
    expect(fetchMock.mock.calls.length).toBe(callsBefore);
  });
});
