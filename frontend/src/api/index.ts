import { createAuthApi } from "./authApi";
import { createChatApi } from "./chatApi";
import { HttpClient } from "./httpClient";

export { createAuthApi } from "./authApi";
export { createChatApi } from "./chatApi";
export { ApiError, HttpClient } from "./httpClient";

export function createStayFlowApi(getAccessToken: () => string | null) {
  const http = new HttpClient({
    baseUrl: import.meta.env.VITE_STAYFLOW_API_URL || "https://bug-free-space-train-w4wvq5wxp4qfv9w9-5243.app.github.dev",
    getAccessToken
  });

  return {
    auth: createAuthApi(http),
    chat: createChatApi(http)
  };
}
