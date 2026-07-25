import { describe, expect, it, vi } from "vitest";
import { HttpClient } from "../src/api/httpClient";
import { createPropertyKnowledgeApi } from "../src/api/propertyKnowledgeApi";
import { PropertyKnowledgeCategory } from "../src/models/propertyKnowledge";

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

describe("propertyKnowledgeApi", () => {
  it("builds list query parameters with the backend contract", async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      successPayload({
        items: [],
        totalCount: 0,
        page: 1,
        pageSize: 10,
        totalPages: 0
      })
    );

    vi.stubGlobal("fetch", fetchMock);

    const api = createPropertyKnowledgeApi(
      new HttpClient({
        baseUrl: "http://test.local",
        getAccessToken: () => "host-token"
      })
    );

    await api.listKnowledge("prop-1", {
      search: " wifi ",
      category: PropertyKnowledgeCategory.WiFi,
      isApproved: true,
      isActive: false,
      pageNumber: 2,
      pageSize: 25
    });

    const [url, options] = fetchMock.mock.calls[0];
    const parsed = new URL(url as string);

    expect(parsed.pathname).toBe("/properties/prop-1/knowledge");
    expect(parsed.searchParams.get("search")).toBe("wifi");
    expect(parsed.searchParams.get("Category")).toBe(String(PropertyKnowledgeCategory.WiFi));
    expect(parsed.searchParams.get("IsApproved")).toBe("true");
    expect(parsed.searchParams.get("IsActive")).toBe("false");
    expect(parsed.searchParams.get("PageNumber")).toBe("2");
    expect(parsed.searchParams.get("PageSize")).toBe("25");
    expect(options.headers).toEqual(expect.objectContaining({ Authorization: "Bearer host-token" }));
  });

  it("calls the expected mutation endpoints", async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce(successPayload({ id: "k-1" }))
      .mockResolvedValueOnce(successPayload({ id: "k-1" }))
      .mockResolvedValueOnce(successPayload({ id: "k-1" }))
      .mockResolvedValueOnce(successPayload({ id: "k-1" }))
      .mockResolvedValueOnce(successPayload({ id: "k-1" }))
      .mockResolvedValueOnce(successPayload({ id: "k-1" }))
      .mockResolvedValueOnce(successPayload({ id: "k-1" }))
      .mockResolvedValueOnce(successPayload({ id: "k-1" }));

    vi.stubGlobal("fetch", fetchMock);

    const api = createPropertyKnowledgeApi(
      new HttpClient({
        baseUrl: "http://test.local",
        getAccessToken: () => "host-token"
      })
    );

    await api.getKnowledgeItem("prop-1", "k-1");
    await api.createKnowledge("prop-1", {
      category: PropertyKnowledgeCategory.Other,
      title: "Title",
      summary: "Summary",
      content: "Content",
      tags: ["wifi"],
      priority: 1,
      isActive: true
    });
    await api.updateKnowledge("prop-1", "k-1", {
      category: PropertyKnowledgeCategory.Other,
      title: "Updated",
      summary: "Summary",
      content: "Content",
      tags: ["wifi"],
      priority: 1,
      isActive: true
    });
    await api.approveKnowledge("prop-1", "k-1");
    await api.unapproveKnowledge("prop-1", "k-1");
    await api.activateKnowledge("prop-1", "k-1");
    await api.deactivateKnowledge("prop-1", "k-1");
    await api.deleteKnowledge("prop-1", "k-1");

    const calledUrls = fetchMock.mock.calls.map((call) => String(call[0]));

    expect(calledUrls).toContain("http://test.local/properties/prop-1/knowledge/k-1");
    expect(calledUrls).toContain("http://test.local/properties/prop-1/knowledge");
    expect(calledUrls).toContain("http://test.local/properties/prop-1/knowledge/k-1/approve");
    expect(calledUrls).toContain("http://test.local/properties/prop-1/knowledge/k-1/unapprove");
    expect(calledUrls).toContain("http://test.local/properties/prop-1/knowledge/k-1/activate");
    expect(calledUrls).toContain("http://test.local/properties/prop-1/knowledge/k-1/deactivate");
  });
});
