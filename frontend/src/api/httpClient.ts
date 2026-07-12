import type { ApiResponse } from "../models/chat";

export class ApiError extends Error {
  constructor(
    message: string,
    public readonly status: number,
    public readonly errors: string[] = []
  ) {
    super(message);
  }
}

export interface HttpClientOptions {
  baseUrl: string;
  getAccessToken?: () => string | null;
  timeoutMs?: number;
}

export class HttpClient {
  private readonly baseUrl: string;
  private readonly timeoutMs: number;

  constructor(private readonly options: HttpClientOptions) {
    this.baseUrl = options.baseUrl.replace(/\/$/, "");
    this.timeoutMs = options.timeoutMs ?? 20000;
  }

  async get<T>(path: string): Promise<T> {
    return this.request<T>(path, { method: "GET" });
  }

  async post<T>(path: string, body?: unknown): Promise<T> {
    return this.request<T>(path, {
      method: "POST",
      body: body === undefined ? undefined : JSON.stringify(body)
    });
  }

  private async request<T>(path: string, init: RequestInit): Promise<T> {
    const controller = new AbortController();
    const timeout = window.setTimeout(() => controller.abort(), this.timeoutMs);
    const token = this.options.getAccessToken?.();

    try {
      const response = await fetch(`${this.baseUrl}${path}`, {
        ...init,
        signal: controller.signal,
        headers: {
          "Content-Type": "application/json",
          ...(token ? { Authorization: `Bearer ${token}` } : {}),
          ...init.headers
        }
      });

      const payload = (await response.json().catch(() => null)) as ApiResponse<T> | null;
      if (!response.ok || !payload?.success) {
        throw new ApiError(safeErrorMessage(response.status, payload?.message), response.status, payload?.errors ?? []);
      }

      if (payload.data === undefined) {
        throw new ApiError("The server response was missing data.", response.status);
      }

      return payload.data;
    } catch (error) {
      if (error instanceof ApiError) {
        throw error;
      }

      if (error instanceof DOMException && error.name === "AbortError") {
        throw new ApiError("The request timed out. Please try again.", 408);
      }

      throw new ApiError("Guest services are temporarily unavailable.", 0);
    } finally {
      window.clearTimeout(timeout);
    }
  }
}

function safeErrorMessage(status: number, serverMessage?: string): string {
  if (status === 401) return "Your session has expired. Please sign in again.";
  if (status === 403) return "You do not have permission to use chat.";
  if (status === 404) return "This conversation is no longer available.";
  if (status === 409) return "This conversation changed. Please refresh and try again.";
  if (status >= 500) return "Guest services are temporarily unavailable.";
  return serverMessage || "We could not complete the request. Please try again.";
}
