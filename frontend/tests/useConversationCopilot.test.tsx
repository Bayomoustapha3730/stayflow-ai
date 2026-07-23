import { act, renderHook, waitFor } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { useConversationCopilot } from "../src/hooks/useConversationCopilot";

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

describe("useConversationCopilot", () => {
  it("ignores stale generated reply after rapid conversation switch", async () => {
    vi.stubEnv("VITE_STAYFLOW_API_URL", "http://test.local");

    const fetchMock = vi.fn().mockImplementation((url: string) => {
      if (url.includes("/copilot/conversations/c-1/generate-reply")) {
        return new Promise((resolve) => {
          setTimeout(() => {
            resolve(ok({
              conversationId: "c-1",
              suggestedReply: "Delayed reply for c-1",
              contextMessageCount: 2,
              isFallback: false,
              generatedAt: "2026-07-23T00:00:00Z"
            }));
          }, 80);
        });
      }

      if (url.includes("/copilot/conversations/c-2/summary")) {
        return Promise.resolve(ok({
          conversationId: "c-2",
          summary: "Summary for c-2",
          visibleMessageCount: 1,
          generatedAt: "2026-07-23T00:00:00Z"
        }));
      }

      if (url.includes("/copilot/conversations/c-2/suggested-replies")) {
        return Promise.resolve(ok({
          conversationId: "c-2",
          suggestedReplies: ["One", "Two", "Three"],
          contextMessageCount: 1,
          generatedAt: "2026-07-23T00:00:00Z"
        }));
      }

      if (url.includes("/copilot/conversations/c-1/summary")) {
        return Promise.resolve(ok({
          conversationId: "c-1",
          summary: "Summary for c-1",
          visibleMessageCount: 1,
          generatedAt: "2026-07-23T00:00:00Z"
        }));
      }

      if (url.includes("/copilot/conversations/c-1/suggested-replies")) {
        return Promise.resolve(ok({
          conversationId: "c-1",
          suggestedReplies: ["One", "Two", "Three"],
          contextMessageCount: 1,
          generatedAt: "2026-07-23T00:00:00Z"
        }));
      }

      return Promise.resolve(ok({}));
    });

    vi.stubGlobal("fetch", fetchMock);

    const onUnauthorized = vi.fn();
    const { result, rerender } = renderHook(
      ({ conversationId }: { conversationId: string }) => useConversationCopilot({
        conversationId,
        accessToken: "token",
        onUnauthorized
      }),
      { initialProps: { conversationId: "c-1" } }
    );

    await act(async () => {
      void result.current.generateReply({ guidance: "Test" });
    });

    rerender({ conversationId: "c-2" });

    await waitFor(() => expect(result.current.summary?.conversationId).toBe("c-2"));
    await waitFor(() => expect(result.current.generatedReply).toBe(""));
    expect(onUnauthorized).not.toHaveBeenCalled();
  });
});
