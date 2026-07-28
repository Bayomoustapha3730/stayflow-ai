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

  it("maps backend-free 404 responses to a generic resource message", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue({
        ok: false,
        status: 404,
        json: async () => ({
          success: false,
          message: "",
          errors: [],
          correlationId: "correlation"
        })
      })
    );

    const http = new HttpClient({ baseUrl: "http://localhost:5243" });
    await expect(http.get("/chat/test")).rejects.toMatchObject({
      message: "The requested resource could not be found.",
      status: 404
    });
  });

  it("prefers a safe backend error message over the generic fallback", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue({
        ok: false,
        status: 422,
        json: async () => ({
          success: false,
          message: "",
          errors: ["Title is required."],
          correlationId: "correlation"
        })
      })
    );

    const http = new HttpClient({ baseUrl: "http://localhost:5243" });
    await expect(http.get("/chat/test")).rejects.toMatchObject({
      message: "Title is required.",
      status: 422
    });
  });

  it("maps common HTTP statuses to generic messages", async () => {
    const fetchMock = vi.fn().mockResolvedValue({
      ok: false,
      status: 403,
      json: async () => ({
        success: false,
        message: "",
        errors: [],
        correlationId: "correlation"
      })
    });

    vi.stubGlobal("fetch", fetchMock);

    const http = new HttpClient({ baseUrl: "http://localhost:5243" });
    await expect(http.get("/chat/test")).rejects.toMatchObject({
      message: "You do not have permission to perform this action.",
      status: 403
    });

    fetchMock.mockResolvedValueOnce({
      ok: false,
      status: 500,
      json: async () => ({
        success: false,
        message: "",
        errors: [],
        correlationId: "correlation"
      })
    });

    await expect(http.get("/chat/test")).rejects.toMatchObject({
      message: "The server encountered an unexpected error.",
      status: 500
    });
  });

  it("does not expose bearer tokens in error messages", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue({
        ok: false,
        status: 500,
        json: async () => ({
          success: false,
          message: "",
          errors: [],
          correlationId: "correlation"
        })
      })
    );

    const http = new HttpClient({
      baseUrl: "http://localhost:5243",
      getAccessToken: () => "secret-token"
    });

    await expect(http.get("/chat/test")).rejects.toMatchObject({
      message: "The server encountered an unexpected error."
    });
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

    const http = new HttpClient({ baseUrl: "https://bug-free-space-train-w4wvq5wxp4qfv9w9.github.dev/" });
    await expect(http.get("/chat/test")).rejects.toMatchObject({
      name: "Error",
      message: "Your session has expired."
    });
  });

  it("does not expose raw html in backend messages", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue({
        ok: false,
        status: 400,
        json: async () => ({
          success: false,
          message: "<script>alert(1)</script><b>Invalid</b>",
          errors: [],
          correlationId: "correlation"
        })
      })
    );

    const http = new HttpClient({ baseUrl: "http://localhost:5243" });
    await expect(http.get("/chat/test")).rejects.toMatchObject({
      message: "Invalid",
      status: 400
    });
  });
});
