import type { ApiResponse } from "../models/chat";
import { extractSafeBackendErrorMessage, getGenericHttpErrorMessage } from "../utils/httpErrorMessages";

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

export interface HttpRequestOptions {
  signal?: AbortSignal;
  headers?: HeadersInit;
}

export class HttpClient {
  private readonly baseUrl: string;
  private readonly timeoutMs: number;

  constructor(private readonly options: HttpClientOptions) {
    this.baseUrl = options.baseUrl.replace(/\/$/, "");
    this.timeoutMs = options.timeoutMs ?? 20000;
  }

  async get<T>(path: string, options?: HttpRequestOptions): Promise<T> {
    return this.request<T>(path, { method: "GET" }, options);
  }

  async post<T>(path: string, body?: unknown, options?: HttpRequestOptions): Promise<T> {
    return this.request<T>(path, {
      method: "POST",
      body: body === undefined ? undefined : JSON.stringify(body)
    }, options);
  }

  async put<T>(path: string, body?: unknown, options?: HttpRequestOptions): Promise<T> {
    return this.request<T>(path, {
      method: "PUT",
      body: body === undefined ? undefined : JSON.stringify(body)
    }, options);
  }

  async delete<T>(path: string, options?: HttpRequestOptions): Promise<T> {
    return this.request<T>(path, { method: "DELETE" }, options);
  }

  private async request<T>(path: string, init: RequestInit, options?: HttpRequestOptions): Promise<T> {
    const controller = new AbortController();
    const timeout = window.setTimeout(() => controller.abort(), this.timeoutMs);
    const token = this.options.getAccessToken?.();
    const signal = options?.signal ?? controller.signal;

    try {
      const response = await fetch(`${this.baseUrl}${path}`, {
        ...init,
        signal,
        headers: {
          "Content-Type": "application/json",
          ...(token ? { Authorization: `Bearer ${token}` } : {}),
          ...(options?.headers ?? {}),
          ...init.headers
        }
      });

      const payload = (await response.json().catch(() => null)) as ApiResponse<T> | null;
      if (!response.ok || !payload?.success) {
        const safeMessage = extractSafeBackendErrorMessage(payload?.message, payload?.errors);
        const message = response.status === 401
          ? getGenericHttpErrorMessage(response.status)
          : safeMessage ?? getGenericHttpErrorMessage(response.status);

        throw new ApiError(message, response.status, payload?.errors ?? []);
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

      throw new ApiError(getGenericHttpErrorMessage(0), 0);
    } finally {
      window.clearTimeout(timeout);
    }
  }
}
