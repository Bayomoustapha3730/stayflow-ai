import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { InvitationDecisionPage } from "../src/pages/InvitationDecisionPage";
import { HostAuthProvider } from "../src/providers/HostAuthProvider";

const syntheticInvitationToken = "test-invitation-token";

function apiSuccess<T>(data: T) {
  return {
    ok: true,
    status: 200,
    json: async () => ({ success: true, message: "ok", data, errors: [], correlationId: "cid" })
  };
}

describe("InvitationDecisionPage", () => {
  beforeEach(() => {
    sessionStorage.clear();
    vi.stubEnv("VITE_STAYFLOW_API_URL", "http://test.local");
    window.history.pushState({}, "", `/onboarding/team?token=${syntheticInvitationToken}`);
  });

  it("registers a new invitee with the retained invitation token and establishes the shared session", async () => {
    const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input);
      if (url.endsWith("/auth/register")) {
        return apiSuccess({ accessToken: "registered-access", refreshToken: "registered-refresh", expiresAt: "2026-09-18T00:00:00Z" });
      }
      if (url.endsWith("/auth/me")) {
        return apiSuccess({ companyId: "company-b", id: "user-b", fullName: "Invited User" });
      }
      if (url.endsWith("/api/onboarding/status")) {
        return apiSuccess({ isCompleted: true });
      }
      throw new Error(`Unexpected request: ${url} ${String(init?.body ?? "")}`);
    });
    vi.stubGlobal("fetch", fetchMock);
    const user = userEvent.setup();

    render(<HostAuthProvider><InvitationDecisionPage /></HostAuthProvider>);

    await user.click(screen.getByRole("button", { name: "Create account" }));
    await user.type(screen.getByLabelText("Full name"), "Invited User");
    await user.type(screen.getByLabelText("Email"), "invitee@example.test");
    await user.type(screen.getByLabelText("Phone number"), "+254700000009");
    await user.type(screen.getByLabelText("Password"), "StrongPassword1!");
    await user.click(screen.getByRole("button", { name: "Create account" }));

    await waitFor(() => expect(sessionStorage.getItem("stayflow.host.accessToken")).toBe("registered-access"));
    const registrationCall = fetchMock.mock.calls.find(([input]) => String(input).endsWith("/auth/register"));
    expect(registrationCall).toBeDefined();
    expect(JSON.parse(String(registrationCall?.[1]?.body))).toMatchObject({ invitationToken: syntheticInvitationToken });
  });
});
