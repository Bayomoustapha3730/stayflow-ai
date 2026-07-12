import { describe, expect, it, vi } from "vitest";
import { ApiError, HttpClient } from "../src/api";

describe("HttpClient", () => {
  it("sends bearer tokens without exposing them in errors", async () => {
    const fetchMock = vi.fn().mockResolvedValue({
      ok: true,
      status: 200,
      json: async () => ({
        success: true,
        message: "ok",
        data: { value: 1 },
        errors: [],
        correlationId: "correlation"
      })
    });
    vi.stubGlobal("fetch", fetchMock);

    const http = new HttpClient({ baseUrl: "http://localhost:5243", getAccessToken: () => "secret-token" });
    await expect(http.get<{ value: number }>("/chat/test")).resolves.toEqual({ value: 1 });

    expect(fetchMock).toHaveBeenCalledWith(
      "http://localhost:5243/chat/test",
      expect.objectContaining({
        headers: expect.objectContaining({ Authorization: "Bearer secret-token" })
      })
    );
  });

  it("normalizes unauthorized API responses", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue({
        ok: false,
        status: 401,
        json: async () => ({
          success: false,
          message: "raw server detail",
          errors: [],
          correlationId: "correlation"
        })
      })
    );

    const http = new HttpClient({ baseUrl: "http://localhost:5243" });
    await expect(http.get("/chat/test")).rejects.toMatchObject({
      name: "Error",
      message: expect.stringContaining("Your session has expired")
    });
  });
});
