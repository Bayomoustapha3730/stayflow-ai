import { describe, expect, it, vi } from "vitest";
import { HttpClient } from "../src/api/httpClient";
import { createOrganizationApi } from "../src/api/organizationApi";

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

describe("organizationApi", () => {
  it("calls current organization endpoints", async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce(successPayload({ id: "org-1", name: "Org", slug: "org", status: "Active" }))
      .mockResolvedValueOnce(successPayload({ id: "org-1", name: "Org 2", slug: "org", status: "Active" }))
      .mockResolvedValueOnce(successPayload([]));

    vi.stubGlobal("fetch", fetchMock);

    const api = createOrganizationApi(
      new HttpClient({
        baseUrl: "http://test.local",
        getAccessToken: () => "host-token"
      })
    );

    await api.getCurrent();
    await api.updateCurrent({ name: "Org 2", slug: "org" });
    await api.listMembers();

    const calledUrls = fetchMock.mock.calls.map((call) => String(call[0]));
    expect(calledUrls).toContain("http://test.local/organization/current");
    expect(calledUrls).toContain("http://test.local/organization/current/members");
  });

  it("calls member role and removal endpoints", async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce(successPayload({ userId: "u-1", role: "Host", status: "Active" }))
      .mockResolvedValueOnce(successPayload({ memberUserId: "u-1" }));

    vi.stubGlobal("fetch", fetchMock);

    const api = createOrganizationApi(
      new HttpClient({
        baseUrl: "http://test.local",
        getAccessToken: () => "host-token"
      })
    );

    await api.updateMemberRole("u-1", "Host");
    await api.removeMember("u-1");

    const calledUrls = fetchMock.mock.calls.map((call) => String(call[0]));
    expect(calledUrls).toContain("http://test.local/organization/current/members/u-1/role");
    expect(calledUrls).toContain("http://test.local/organization/current/members/u-1");
  });
});