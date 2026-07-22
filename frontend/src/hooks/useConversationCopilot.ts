import { useCallback, useEffect, useMemo, useState } from "react";
import { ApiError, HttpClient, createHostCopilotApi } from "../api";
import type {
  ConversationCopilotSuggestionsResponse,
  ConversationCopilotSummaryResponse,
  CopilotTone,
  CopilotUrgency
} from "../models/copilot";

interface UseConversationCopilotOptions {
  conversationId: string | null;
  accessToken: string | null;
  onUnauthorized: () => void;
}

export interface UseConversationCopilotResult {
  summary: ConversationCopilotSummaryResponse | null;
  suggestions: string[];
  tone: CopilotTone;
  isLoadingSummary: boolean;
  isLoadingSuggestions: boolean;
  isRefreshing: boolean;
  summaryError: string | null;
  suggestionsError: string | null;
  setTone: (tone: CopilotTone) => void;
  refreshAll: () => Promise<void>;
  retrySummary: () => Promise<void>;
  retrySuggestions: () => Promise<void>;
}

export function useConversationCopilot({
  conversationId,
  accessToken,
  onUnauthorized
}: UseConversationCopilotOptions): UseConversationCopilotResult {
  const [summary, setSummary] = useState<ConversationCopilotSummaryResponse | null>(null);
  const [suggestions, setSuggestions] = useState<string[]>([]);
  const [tone, setTone] = useState<CopilotTone>("professional");
  const [isLoadingSummary, setIsLoadingSummary] = useState(false);
  const [isLoadingSuggestions, setIsLoadingSuggestions] = useState(false);
  const [isRefreshing, setIsRefreshing] = useState(false);
  const [summaryError, setSummaryError] = useState<string | null>(null);
  const [suggestionsError, setSuggestionsError] = useState<string | null>(null);

  const http = useMemo(
    () =>
      new HttpClient({
        baseUrl: import.meta.env.VITE_STAYFLOW_API_URL ?? "http://localhost:5243",
        getAccessToken: () => accessToken
      }),
    [accessToken]
  );

  const api = useMemo(() => createHostCopilotApi(http), [http]);

  const normalizeSummary = useCallback((response: ConversationCopilotSummaryResponse): ConversationCopilotSummaryResponse => {
    const safeSummary = typeof response.summary === "string" && response.summary.trim().length > 0
      ? response.summary
      : "No summary available yet.";
    const guestIntent = response.guestIntent?.trim() || deriveGuestIntent(response.latestGuestMessage);
    const importantFacts = response.importantFacts?.length
      ? response.importantFacts
      : deriveImportantFacts(response);
    const urgency = response.urgency ?? deriveUrgency(response.latestGuestMessage);

    return {
      ...response,
      summary: safeSummary,
      visibleMessageCount: Number.isFinite(response.visibleMessageCount) ? response.visibleMessageCount : 0,
      guestIntent,
      importantFacts,
      urgency
    };
  }, []);

  const loadSummary = useCallback(async () => {
    if (!conversationId || !accessToken) {
      setSummary(null);
      setSummaryError(null);
      setIsLoadingSummary(false);
      return;
    }

    setIsLoadingSummary(true);
    setSummaryError(null);
    try {
      const response = await api.getConversationSummary(conversationId);
      setSummary(normalizeSummary(response));
    } catch (failure) {
      if (failure instanceof ApiError && failure.status === 401) {
        onUnauthorized();
        return;
      }

      setSummaryError(
        failure instanceof Error
          ? failure.message
          : "Unable to load the conversation summary."
      );
    } finally {
      setIsLoadingSummary(false);
    }
  }, [accessToken, api, conversationId, normalizeSummary, onUnauthorized]);

  const loadSuggestions = useCallback(async () => {
    if (!conversationId || !accessToken) {
      setSuggestions([]);
      setSuggestionsError(null);
      setIsLoadingSuggestions(false);
      return;
    }

    setIsLoadingSuggestions(true);
    setSuggestionsError(null);
    try {
      const response: ConversationCopilotSuggestionsResponse = await api.getSuggestedReplies(conversationId, tone);
      setSuggestions(Array.isArray(response.suggestedReplies) ? response.suggestedReplies : []);
    } catch (failure) {
      if (failure instanceof ApiError && failure.status === 401) {
        onUnauthorized();
        return;
      }

      setSuggestionsError(
        failure instanceof Error
          ? failure.message
          : "Unable to load suggested replies."
      );
      setSuggestions([]);
    } finally {
      setIsLoadingSuggestions(false);
    }
  }, [accessToken, api, conversationId, onUnauthorized, tone]);

  const refreshAll = useCallback(async () => {
    if (!conversationId || !accessToken) {
      return;
    }

    setIsRefreshing(true);
    try {
      await Promise.all([loadSummary(), loadSuggestions()]);
    } finally {
      setIsRefreshing(false);
    }
  }, [accessToken, conversationId, loadSummary, loadSuggestions]);

  useEffect(() => {
    if (!conversationId || !accessToken) {
      setSummary(null);
      setSuggestions([]);
      setSummaryError(null);
      setSuggestionsError(null);
      return;
    }

    void loadSummary();
  }, [accessToken, conversationId, loadSummary]);

  useEffect(() => {
    if (!conversationId || !accessToken) {
      return;
    }

    void loadSuggestions();
  }, [accessToken, conversationId, loadSuggestions, tone]);

  return {
    summary,
    suggestions,
    tone,
    isLoadingSummary,
    isLoadingSuggestions,
    isRefreshing,
    summaryError,
    suggestionsError,
    setTone,
    refreshAll,
    retrySummary: loadSummary,
    retrySuggestions: loadSuggestions
  };
}

function deriveGuestIntent(latestGuestMessage?: string | null): string {
  const text = (latestGuestMessage ?? "").toLowerCase();

  if (text.includes("check-in") || text.includes("check in")) return "Check-in assistance";
  if (text.includes("check-out") || text.includes("check out") || text.includes("checkout")) return "Check-out support";
  if (text.includes("wifi") || text.includes("wi-fi") || text.includes("internet")) return "Connectivity help";
  if (text.includes("parking")) return "Parking guidance";
  if (text.includes("late") || text.includes("extend")) return "Schedule flexibility request";
  return "General stay support";
}

function deriveUrgency(latestGuestMessage?: string | null): CopilotUrgency {
  const text = (latestGuestMessage ?? "").toLowerCase();
  if (text.includes("urgent") || text.includes("asap") || text.includes("immediately") || text.includes("emergency")) {
    return "high";
  }

  if (text.includes("today") || text.includes("now") || text.includes("soon")) {
    return "medium";
  }

  return "low";
}

function deriveImportantFacts(summary: ConversationCopilotSummaryResponse): string[] {
  const facts: string[] = [];
  if (summary.visibleMessageCount > 0) {
    facts.push(`${summary.visibleMessageCount} visible message${summary.visibleMessageCount === 1 ? "" : "s"}`);
  }

  if (summary.latestGuestMessage?.trim()) {
    facts.push(`Latest guest message: ${summary.latestGuestMessage.trim()}`);
  }

  if (facts.length === 0) {
    facts.push("No important facts detected yet.");
  }

  return facts;
}