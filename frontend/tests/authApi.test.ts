import { describe, expect, it, vi } from "vitest";
import { createAuthApi } from "../src/api/authApi";
import { HttpClient } from "../src/api/httpClient";

function successPayload<T>(data: T) {
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

describe("authApi", () => {
  it("calls password reset, verification, and session endpoints", async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce(successPayload({}))
      .mockResolvedValueOnce(successPayload({}))
      .mockResolvedValueOnce(successPayload({ verificationToken: "", expiresAtUtc: "2026-08-06T00:00:00Z" }))
      .mockResolvedValueOnce(successPayload({ verificationToken: "", expiresAtUtc: "2026-08-06T00:00:00Z" }))
      .mockResolvedValueOnce(successPayload([{ sessionId: "session-1", createdAtUtc: "2026-08-06T00:00:00Z", expiresAtUtc: "2026-08-07T00:00:00Z", isCurrent: true }]))
      .mockResolvedValueOnce(successPayload({}))
      .mockResolvedValueOnce(successPayload({}));

    vi.stubGlobal("fetch", fetchMock);

    const api = createAuthApi(new HttpClient({
      baseUrl: "http://test.local",
      getAccessToken: () => "host-token"
    }));

    await api.requestPasswordReset("host@example.com");
    await api.confirmPasswordReset({ token: "token", newPassword: "New Password 123!" });
    await api.requestEmailVerification();
    await api.resendEmailVerification();
    await api.listSessions();
    await api.revokeSession("session-1");
    await api.revokeAllSessions();

    const calledUrls = fetchMock.mock.calls.map((call) => String(call[0]));
    expect(calledUrls).toContain("http://test.local/auth/password-reset");
    expect(calledUrls).toContain("http://test.local/auth/password-reset/confirm");
    expect(calledUrls).toContain("http://test.local/auth/email-verification");
    expect(calledUrls).toContain("http://test.local/auth/email-verification/resend");
    expect(calledUrls).toContain("http://test.local/auth/sessions");
    expect(calledUrls).toContain("http://test.local/auth/sessions/session-1/revoke");
    expect(calledUrls).toContain("http://test.local/auth/sessions/revoke-all");
  });
});