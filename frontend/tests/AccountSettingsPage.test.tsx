import { StrictMode } from "react";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

const mockAuthState = {
  accessToken: "token",
  currentUser: {
    id: "u1",
    companyId: "c1",
    fullName: "Demo User",
    email: "demo.user@stayflow.local",
    phoneNumber: "+254700000001",
    preferredLanguage: "en",
    timeZone: "UTC",
    isEmailVerified: true,
    emailNotificationsEnabled: true,
    securityNotificationsEnabled: true,
    productUpdatesEnabled: false,
    organizationRole: "Owner",
    roles: ["Demo Administrator"],
    permissions: ["auth.me"]
  },
  isAuthenticated: true,
  isSigningIn: false,
  error: null as string | null,
  login: vi.fn(async () => {}),
  logout: vi.fn(),
  clearError: vi.fn(),
  refreshCurrentUser: vi.fn(async () => {}),
  setCurrentUserProfile: vi.fn()
};

vi.mock("../src/hooks/useHostAuth", () => ({
  useHostAuth: () => mockAuthState
}));

vi.mock("../src/components/host", () => ({
  HostConsoleNav: () => <div data-testid="host-console-nav" />,
  HostLoginPanel: () => <div data-testid="host-login-panel" />
}));

import { AccountSettingsPage } from "../src/pages/AccountSettingsPage";

function apiSuccess<T>(data: T) {
  return {
    ok: true,
    status: 200,
    headers: {
      get: () => null
    },
    json: async () => ({
      success: true,
      message: "ok",
      data,
      errors: [],
      correlationId: "cid"
    })
  };
}

function apiFailure(status: number, message: string) {
  return {
    ok: false,
    status,
    headers: {
      get: (name: string) => (name.toLowerCase() === "retry-after" ? "5" : null)
    },
    json: async () => ({
      success: false,
      message,
      data: null,
      errors: [message],
      correlationId: "cid"
    })
  };
}

describe("AccountSettingsPage sessions lifecycle", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("loads sessions once on mount and does not request-storm on rerender", async () => {
    const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = typeof input === "string" ? input : input.toString();
      const method = init?.method ?? "GET";
      if (url.endsWith("/auth/sessions") && method === "GET") {
        return apiSuccess([]);
      }

      if (url.endsWith("/auth/me") && method === "PUT") {
        return apiSuccess(mockAuthState.currentUser);
      }

      if (url.endsWith("/auth/change-password") && method === "POST") {
        return apiSuccess({});
      }

      if (url.endsWith("/auth/email-verification/resend") && method === "POST") {
        return apiSuccess({ verificationToken: "t", expiresAtUtc: new Date().toISOString() });
      }

      return apiSuccess({});
    });

    vi.stubGlobal("fetch", fetchMock);

    const { rerender } = render(
      <StrictMode>
        <AccountSettingsPage />
      </StrictMode>
    );

    await waitFor(() => {
      const listCalls = fetchMock.mock.calls.filter(([input, init]) => {
        const url = typeof input === "string" ? input : input.toString();
        const method = init?.method ?? "GET";
        return url.endsWith("/auth/sessions") && method === "GET";
      });
      expect(listCalls.length).toBeGreaterThanOrEqual(1);
      expect(listCalls.length).toBeLessThanOrEqual(2);
    });

    rerender(
      <StrictMode>
        <AccountSettingsPage />
      </StrictMode>
    );

    await waitFor(() => {
      const listCalls = fetchMock.mock.calls.filter(([input, init]) => {
        const url = typeof input === "string" ? input : input.toString();
        const method = init?.method ?? "GET";
        return url.endsWith("/auth/sessions") && method === "GET";
      });

      expect(listCalls.length).toBeLessThanOrEqual(2);
    });
  });

  it("revoke session triggers only one bounded sessions refresh", async () => {
    const sessionsPayload = [
      {
        sessionId: "session-1",
        createdAtUtc: "2026-08-10T00:00:00Z",
        lastUsedAtUtc: "2026-08-10T01:00:00Z",
        expiresAtUtc: "2026-08-20T00:00:00Z",
        isCurrent: false
      }
    ];

    const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = typeof input === "string" ? input : input.toString();
      const method = init?.method ?? "GET";

      if (url.endsWith("/auth/sessions") && method === "GET") {
        return apiSuccess(sessionsPayload);
      }

      if (url.endsWith("/auth/sessions/session-1/revoke") && method === "POST") {
        return apiSuccess({});
      }

      return apiSuccess({});
    });

    vi.stubGlobal("fetch", fetchMock);

    render(<AccountSettingsPage />);

    await screen.findByText("Active session");

    fireEvent.click(screen.getByRole("button", { name: "Revoke" }));

    await waitFor(() => {
      const revokeCalls = fetchMock.mock.calls.filter(([input, init]) => {
        const url = typeof input === "string" ? input : input.toString();
        const method = init?.method ?? "GET";
        return url.endsWith("/auth/sessions/session-1/revoke") && method === "POST";
      });
      expect(revokeCalls).toHaveLength(1);

      const listCalls = fetchMock.mock.calls.filter(([input, init]) => {
        const url = typeof input === "string" ? input : input.toString();
        const method = init?.method ?? "GET";
        return url.endsWith("/auth/sessions") && method === "GET";
      });
      expect(listCalls).toHaveLength(1);
    });
  });

  it("blocks duplicate revoke clicks while one revoke is in-flight", async () => {
    const sessionsPayload = [
      {
        sessionId: "session-1",
        createdAtUtc: "2026-08-10T00:00:00Z",
        lastUsedAtUtc: "2026-08-10T01:00:00Z",
        expiresAtUtc: "2026-08-20T00:00:00Z",
        isCurrent: false
      }
    ];

    let resolveRevoke!: () => void;
    const revokePromise = new Promise((resolve) => {
      resolveRevoke = () => resolve(apiSuccess({}));
    });

    const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = typeof input === "string" ? input : input.toString();
      const method = init?.method ?? "GET";

      if (url.endsWith("/auth/sessions") && method === "GET") {
        return apiSuccess(sessionsPayload);
      }

      if (url.endsWith("/auth/sessions/session-1/revoke") && method === "POST") {
        return revokePromise;
      }

      return apiSuccess({});
    });

    vi.stubGlobal("fetch", fetchMock);

    render(<AccountSettingsPage />);

    const revokeButton = await screen.findByRole("button", { name: "Revoke" });
    fireEvent.click(revokeButton);
    fireEvent.click(revokeButton);

    expect(fetchMock.mock.calls.filter(([input, init]) => {
      const url = typeof input === "string" ? input : input.toString();
      const method = init?.method ?? "GET";
      return url.endsWith("/auth/sessions/session-1/revoke") && method === "POST";
    })).toHaveLength(1);

    expect(await screen.findByRole("button", { name: "Revoking..." })).toBeDisabled();

    resolveRevoke();

    await waitFor(() => {
      expect(screen.queryByText("Active session")).not.toBeInTheDocument();
      expect(screen.getByText("Session revoked.")).toBeInTheDocument();
    });
  });

  it("revoke all sessions clears list without triggering an extra list fetch", async () => {
    const sessionsPayload = [
      {
        sessionId: "session-1",
        createdAtUtc: "2026-08-10T00:00:00Z",
        lastUsedAtUtc: "2026-08-10T01:00:00Z",
        expiresAtUtc: "2026-08-20T00:00:00Z",
        isCurrent: false
      },
      {
        sessionId: "session-2",
        createdAtUtc: "2026-08-10T00:00:00Z",
        lastUsedAtUtc: "2026-08-10T01:00:00Z",
        expiresAtUtc: "2026-08-20T00:00:00Z",
        isCurrent: true
      }
    ];

    const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = typeof input === "string" ? input : input.toString();
      const method = init?.method ?? "GET";

      if (url.endsWith("/auth/sessions") && method === "GET") {
        return apiSuccess(sessionsPayload);
      }

      if (url.endsWith("/auth/sessions/revoke-all") && method === "POST") {
        return apiSuccess({});
      }

      return apiSuccess({});
    });

    vi.stubGlobal("fetch", fetchMock);

    render(<AccountSettingsPage />);

    await screen.findByText("Active session");

    fireEvent.click(screen.getByRole("button", { name: "Revoke All Sessions" }));

    await waitFor(() => {
      expect(screen.getByText("All refresh sessions revoked.")).toBeInTheDocument();
      expect(screen.getByText("No active refresh sessions found.")).toBeInTheDocument();
    });

    const listCalls = fetchMock.mock.calls.filter(([input, init]) => {
      const url = typeof input === "string" ? input : input.toString();
      const method = init?.method ?? "GET";
      return url.endsWith("/auth/sessions") && method === "GET";
    });
    expect(listCalls).toHaveLength(1);

    const revokeAllCalls = fetchMock.mock.calls.filter(([input, init]) => {
      const url = typeof input === "string" ? input : input.toString();
      const method = init?.method ?? "GET";
      return url.endsWith("/auth/sessions/revoke-all") && method === "POST";
    });
    expect(revokeAllCalls).toHaveLength(1);
  });

  it("429 on sessions does not auto-retry in a loop", async () => {
    const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = typeof input === "string" ? input : input.toString();
      const method = init?.method ?? "GET";

      if (url.endsWith("/auth/sessions") && method === "GET") {
        return apiFailure(429, "Too many requests. Please try again shortly.");
      }

      return apiSuccess({});
    });

    vi.stubGlobal("fetch", fetchMock);

    render(<AccountSettingsPage />);

    await screen.findByText("Too many requests. Please try again shortly.");

    const listCalls = fetchMock.mock.calls.filter(([input, init]) => {
      const url = typeof input === "string" ? input : input.toString();
      const method = init?.method ?? "GET";
      return url.endsWith("/auth/sessions") && method === "GET";
    });

    expect(listCalls.length).toBe(1);
  });
});
