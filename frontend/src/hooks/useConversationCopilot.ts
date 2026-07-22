import { useCallback, useMemo, useState } from "react";
import { ApiError, HttpClient, createHostCopilotApi } from "../api";
import type { CopilotSuggestReplyResponse } from "../models/copilot";

interface UseConversationCopilotOptions {
  conversationId: string | null;
  accessToken: string | null;
  onUnauthorized: () => void;
}

export interface UseConversationCopilotResult {
  suggestion: CopilotSuggestReplyResponse | null;
  isGenerating: boolean;
  error: string | null;
  generateSuggestion: (guidance?: string) => Promise<string | null>;
  clearSuggestion: () => void;
  clearError: () => void;
}

export function useConversationCopilot({
  conversationId,
  accessToken,
  onUnauthorized
}: UseConversationCopilotOptions): UseConversationCopilotResult {
  const [suggestion, setSuggestion] = useState<CopilotSuggestReplyResponse | null>(null);
  const [isGenerating, setIsGenerating] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const http = useMemo(
    () =>
      new HttpClient({
        baseUrl: import.meta.env.VITE_STAYFLOW_API_URL ?? "http://localhost:5243",
        getAccessToken: () => accessToken
      }),
    [accessToken]
  );

  const api = useMemo(() => createHostCopilotApi(http), [http]);

  const clearSuggestion = useCallback(() => {
    setSuggestion(null);
  }, []);

  const clearError = useCallback(() => {
    setError(null);
  }, []);

  const generateSuggestion = useCallback(
    async (guidance?: string): Promise<string | null> => {
      if (!conversationId || !accessToken || isGenerating) {
        return null;
      }

      setIsGenerating(true);
      setError(null);

      try {
        const response = await api.suggestReply(conversationId, {
          guidance: guidance?.trim() || undefined,
          includeInternalNotes: true,
          maxContextMessages: 16
        });
        setSuggestion(response);
        return response.suggestedReply;
      } catch (failure) {
        if (failure instanceof ApiError && failure.status === 401) {
          onUnauthorized();
          return null;
        }

        const message = failure instanceof Error ? failure.message : "Unable to generate a Copilot draft right now.";
        setError(message);
        return null;
      } finally {
        setIsGenerating(false);
      }
    },
    [accessToken, api, conversationId, isGenerating, onUnauthorized]
  );

  return {
    suggestion,
    isGenerating,
    error,
    generateSuggestion,
    clearSuggestion,
    clearError
  };
}