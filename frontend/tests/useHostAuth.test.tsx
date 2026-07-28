import { act, renderHook } from "@testing-library/react";
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
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(loginSuccessResponse()));

    const { result } = renderHook(() => useHostAuth());

    await act(async () => {
      await result.current.login("host@example.com", "Password123!");
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
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(loginSuccessResponse()));

    const { result } = renderHook(() => useHostAuth());

    await act(async () => {
      await result.current.login("host@example.com", "Password123!");
    });

    act(() => {
      result.current.logout();
    });

    expect(result.current.isAuthenticated).toBe(false);
    expect(sessionStorage.getItem("stayflow.host.accessToken")).toBeNull();
  });
});
