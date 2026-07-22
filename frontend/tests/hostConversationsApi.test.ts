import { describe, expect, it, vi } from "vitest";
import { createHostConversationsApi } from "../src/api/hostConversationsApi";
import { HttpClient } from "../src/api/httpClient";
import { ConversationStatus } from "../src/models/enums";

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

describe("hostConversationsApi", () => {
  it("sends Authorization header and query params for listConversations", async () => {
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

    const http = new HttpClient({
      baseUrl: "http://test.local",
      getAccessToken: () => "host-token"
    });

    const api = createHostConversationsApi(http);

    await api.listConversations({
      search: "alice",
      status: ConversationStatus.AwaitingHost,
      requiresHostAttention: true,
      page: 2,
      pageSize: 25
    });

    const [url, options] = fetchMock.mock.calls[0];

    expect(url).toContain("/conversations?");

    const parsed = new URL(url as string);
    expect(parsed.searchParams.get("search")).toBe("alice");
    expect(parsed.searchParams.get("status")).toBe(String(ConversationStatus.AwaitingHost));
    expect(parsed.searchParams.get("requiresHostAttention")).toBe("true");
    expect(parsed.searchParams.get("page")).toBe("2");
    expect(parsed.searchParams.get("pageSize")).toBe("25");

    expect(options.headers).toEqual(
      expect.objectContaining({
        Authorization: "Bearer host-token"
      })
    );
  });

  it("omits undefined and empty query params", async () => {
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

    const api = createHostConversationsApi(
      new HttpClient({
        baseUrl: "http://test.local"
      })
    );

    await api.listConversations({
      search: "   ",
      page: 1,
      pageSize: 10
    });

    const [url] = fetchMock.mock.calls[0];
    const parsed = new URL(url as string);

    expect(parsed.searchParams.get("search")).toBeNull();
    expect(parsed.searchParams.get("status")).toBeNull();
    expect(parsed.searchParams.get("requiresHostAttention")).toBeNull();
  });
});
