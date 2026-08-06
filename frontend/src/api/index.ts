import { createAuthApi } from "./authApi";
import { createChatApi } from "./chatApi";
import { createHostCopilotApi } from "./hostCopilotApi";
import { createHostCopilotWorkspaceApi } from "./hostCopilotWorkspaceApi";
import { createOnboardingApi } from "./onboardingApi";
import { createPropertyKnowledgeApi } from "./propertyKnowledgeApi";
import { HttpClient } from "./httpClient";
import { getRuntimeApiUrl } from "../runtimeConfig";

export { createAuthApi } from "./authApi";
export { createChatApi } from "./chatApi";
export { createHostCopilotApi } from "./hostCopilotApi";
export { createHostCopilotWorkspaceApi } from "./hostCopilotWorkspaceApi";
export { createOnboardingApi } from "./onboardingApi";
export { createPropertyKnowledgeApi } from "./propertyKnowledgeApi";
export { ApiError, HttpClient } from "./httpClient";

export function createStayFlowApi(getAccessToken: () => string | null) {
  const http = new HttpClient({
    baseUrl: getRuntimeApiUrl(),
    getAccessToken
  });

  return {
    auth: createAuthApi(http),
    chat: createChatApi(http),
    hostCopilot: createHostCopilotApi(http),
    hostCopilotWorkspace: createHostCopilotWorkspaceApi(http),
    onboarding: createOnboardingApi(http),
    propertyKnowledge: createPropertyKnowledgeApi(http)
  };
}
