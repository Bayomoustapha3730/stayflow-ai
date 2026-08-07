import { beforeEach, describe, expect, it, vi } from "vitest";
import { HttpClient } from "../src/api/httpClient";
import { createPlatformAdminApi } from "../src/api/platformAdminApi";

function createMockResponse(data: unknown) {
  return {
    ok: true,
    status: 200,
    headers: new Headers(),
    json: async () => ({
      success: true,
      data,
      message: "ok",
      errors: []
    })
  } satisfies Partial<Response>;
}

describe("platformAdminApi", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it("builds tenant listing query parameters", async () => {
    const fetchMock = vi.spyOn(globalThis, "fetch").mockResolvedValue(
      createMockResponse({ items: [], totalCount: 0, page: 1, pageSize: 25 }) as Response
    );

    const http = new HttpClient({
      baseUrl: "http://localhost:5243",
      getAccessToken: () => "token"
    });
    const api = createPlatformAdminApi(http);

    await api.listTenants({ search: "Acme", status: "Active", page: 2, pageSize: 10 });

    expect(fetchMock).toHaveBeenCalledTimes(1);
    const [url, init] = fetchMock.mock.calls[0];
    expect(String(url)).toContain("/api/platform-admin/tenants?");
    expect(String(url)).toContain("search=Acme");
    expect(String(url)).toContain("status=Active");
    expect(String(url)).toContain("page=2");
    expect(String(url)).toContain("pageSize=10");
    expect((init?.headers as Record<string, string>).Authorization).toBe("Bearer token");
  });

  it("calls support impersonation start endpoint", async () => {
    const fetchMock = vi.spyOn(globalThis, "fetch").mockResolvedValue(
      createMockResponse({
        sessionId: "11111111-1111-1111-1111-111111111111",
        targetCompanyId: "22222222-2222-2222-2222-222222222222",
        targetUserId: "33333333-3333-3333-3333-333333333333",
        startedAtUtc: "2026-01-01T00:00:00Z",
        expiresAtUtc: "2026-01-01T00:30:00Z"
      }) as Response
    );

    const http = new HttpClient({ baseUrl: "http://localhost:5243" });
    const api = createPlatformAdminApi(http);

    await api.startSupportImpersonation({
      targetCompanyId: "22222222-2222-2222-2222-222222222222",
      targetUserId: "33333333-3333-3333-3333-333333333333",
      reason: "Support investigation",
      explicitAuthorizationCode: "AUTH-2048"
    });

    expect(fetchMock).toHaveBeenCalledTimes(1);
    const [url] = fetchMock.mock.calls[0];
    expect(String(url)).toContain("/api/platform-admin/support/impersonation/start");
  });
});
