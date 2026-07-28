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

  it("builds message history query safely", async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      successPayload({
        conversationId: "c-1",
        messages: {
          items: [],
          pageNumber: 2,
          pageSize: 50,
          totalCount: 0,
          totalPages: 0
        }
      })
    );

    vi.stubGlobal("fetch", fetchMock);

    const api = createHostConversationsApi(
      new HttpClient({
        baseUrl: "http://test.local",
        getAccessToken: () => "host-token"
      })
    );

    await api.getMessages("c-1", {
      includeInternal: true,
      pageNumber: 2,
      pageSize: 50
    });

    const [url] = fetchMock.mock.calls[0];
    const parsed = new URL(url as string);
    expect(parsed.pathname).toBe("/conversations/c-1/messages");
    expect(parsed.searchParams.get("includeInternal")).toBe("true");
    expect(parsed.searchParams.get("pageNumber")).toBe("2");
    expect(parsed.searchParams.get("pageSize")).toBe("50");
  });

  it("calls all conversation mutation endpoints", async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValue(successPayload({ id: "m-1", conversationId: "c-1", senderType: 2, messageType: 0, content: "Hi", isInternal: false, sentAt: "2026-07-22T00:00:00Z" }))
      .mockResolvedValueOnce(successPayload({ id: "m-2", conversationId: "c-1", senderType: 3, messageType: 3, content: "Note", isInternal: true, sentAt: "2026-07-22T00:00:00Z" }))
      .mockResolvedValueOnce(successPayload({ conversationId: "c-1" }))
      .mockResolvedValueOnce(successPayload({ conversationId: "c-1" }))
      .mockResolvedValueOnce(successPayload({ conversationId: "c-1" }))
      .mockResolvedValueOnce(successPayload({ conversationId: "c-1" }));

    vi.stubGlobal("fetch", fetchMock);

    const api = createHostConversationsApi(
      new HttpClient({
        baseUrl: "http://test.local",
        getAccessToken: () => "host-token"
      })
    );

    await api.addHostMessage("c-1", "Reply");
    await api.addInternalNote("c-1", "Internal");
    await api.enableHumanTakeover("c-1");
    await api.returnToAI("c-1");
    await api.resolveConversation("c-1");
    await api.closeConversation("c-1");

    const calledUrls = fetchMock.mock.calls.map((call) => String(call[0]));

    expect(calledUrls.some((url) => url.endsWith("/conversations/c-1/messages/host"))).toBe(true);
    expect(calledUrls.some((url) => url.endsWith("/conversations/c-1/notes"))).toBe(true);
    expect(calledUrls.some((url) => url.endsWith("/conversations/c-1/human-takeover"))).toBe(true);
    expect(calledUrls.some((url) => url.endsWith("/conversations/c-1/return-to-ai"))).toBe(true);
    expect(calledUrls.some((url) => url.endsWith("/conversations/c-1/resolve"))).toBe(true);
    expect(calledUrls.some((url) => url.endsWith("/conversations/c-1/close"))).toBe(true);
  });
});
