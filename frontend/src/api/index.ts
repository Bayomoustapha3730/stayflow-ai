import { createAuthApi } from "./authApi";
import { createChatApi } from "./chatApi";
import { createHostCopilotApi } from "./hostCopilotApi";
import { createPropertyKnowledgeApi } from "./propertyKnowledgeApi";
import { HttpClient } from "./httpClient";

export { createAuthApi } from "./authApi";
export { createChatApi } from "./chatApi";
export { createHostCopilotApi } from "./hostCopilotApi";
export { createPropertyKnowledgeApi } from "./propertyKnowledgeApi";
export { ApiError, HttpClient } from "./httpClient";

export function createStayFlowApi(getAccessToken: () => string | null) {
  const http = new HttpClient({
    baseUrl: import.meta.env.VITE_STAYFLOW_API_URL || "http://localhost:5243",
    getAccessToken
  });

  return {
    auth: createAuthApi(http),
    chat: createChatApi(http),
    hostCopilot: createHostCopilotApi(http),
    propertyKnowledge: createPropertyKnowledgeApi(http)
  };
}
