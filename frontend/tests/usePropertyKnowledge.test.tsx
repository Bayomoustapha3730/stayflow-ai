import { act, renderHook, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { usePropertyKnowledge } from "../src/hooks/usePropertyKnowledge";

function ok<T>(data: T) {
  return {
    ok: true,
    status: 200,
    json: async () => ({
      success: true,
      message: "ok",
      data,
      errors: [],
      correlationId: "cid"
    })
  };
}

function failure(status: number, message = "", errors: string[] = []) {
  return {
    ok: false,
    status,
    json: async () => ({
      success: false,
      message,
      data: null,
      errors,
      correlationId: "cid"
    })
  };
}

function summaryItem(id = "k-1") {
  return {
    id,
    propertyId: "p-1",
    propertyName: "Westlands Apartment",
    category: 0,
    categoryLabel: "Wi-Fi",
    title: "Wi-Fi",
    summary: "Wi-Fi details",
    tags: ["wifi"],
    priority: 10,
    isApproved: true,
    isActive: true,
    approvedAt: "2026-07-22T00:00:00Z",
    approvedBy: "Host",
    createdAt: "2026-07-22T00:00:00Z",
    updatedAt: "2026-07-22T00:00:00Z",
    canBeUsedByAI: true
  };
}

function detailItem(id = "k-1") {
  return {
    ...summaryItem(id),
    content: "Wi-Fi password is on the router.",
    estimatedCharacterContribution: 42
  };
}

describe("usePropertyKnowledge", () => {
  beforeEach(() => {
    vi.stubEnv("VITE_STAYFLOW_API_URL", "http://test.local");
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("maps a list 404 to the property not found message and retries successfully", async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce(failure(404))
      .mockResolvedValueOnce(ok({
        items: [summaryItem()],
        totalCount: 1,
        pageNumber: 1,
        pageSize: 10,
        totalPages: 1
      }));

    vi.stubGlobal("fetch", fetchMock);

    const onUnauthorized = vi.fn();
    const { result } = renderHook(() =>
      usePropertyKnowledge({
        propertyId: "p-1",
        accessToken: "host-token",
        onUnauthorized
      })
    );

    await waitFor(() => expect(result.current.error).toBe("Property not found."));
    const callsBeforeRetry = fetchMock.mock.calls.length;

    await act(async () => {
      result.current.retry();
    });

    await waitFor(() => expect(fetchMock.mock.calls.length).toBeGreaterThan(callsBeforeRetry));
    expect(onUnauthorized).not.toHaveBeenCalled();
  });

  it("uses the normal property knowledge fallback for general failures", async () => {
    const fetchMock = vi.fn().mockResolvedValue(failure(500));
    vi.stubGlobal("fetch", fetchMock);

    const { result } = renderHook(() =>
      usePropertyKnowledge({
        propertyId: "p-1",
        accessToken: "host-token",
        onUnauthorized: vi.fn()
      })
    );

    await waitFor(() => expect(result.current.error).toBe("Unable to load property knowledge."));
  });
});
