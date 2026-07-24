import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { ApiError, HttpClient, createHostCopilotApi } from "../api";
import type {
  CopilotContextWarning,
  ConversationCopilotSuggestionsResponse,
  ConversationCopilotSummaryResponse,
  CopilotSuggestReplyResponse,
  CopilotSuggestReplyRequest,
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
  suggestionMetadata: ConversationCopilotSuggestionsResponse | null;
  generatedReply: CopilotSuggestReplyResponse | null;
  generatedReplyDraft: string;
  generatedReplyTone: CopilotTone | null;
  tone: CopilotTone;
  isLoadingSummary: boolean;
  isLoadingSuggestions: boolean;
  isGeneratingReply: boolean;
  isRefreshing: boolean;
  summaryError: string | null;
  suggestionsError: string | null;
  generatedReplyError: string | null;
  setTone: (tone: CopilotTone) => void;
  setGeneratedReplyDraft: (value: string) => void;
  generateReply: (request?: CopilotSuggestReplyRequest) => Promise<void>;
  clearGeneratedReply: () => void;
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
  const [suggestionMetadata, setSuggestionMetadata] = useState<ConversationCopilotSuggestionsResponse | null>(null);
  const [generatedReply, setGeneratedReply] = useState<CopilotSuggestReplyResponse | null>(null);
  const [generatedReplyDraft, setGeneratedReplyDraft] = useState("");
  const [generatedReplyTone, setGeneratedReplyTone] = useState<CopilotTone | null>(null);
  const [tone, setTone] = useState<CopilotTone>("professional");
  const [isLoadingSummary, setIsLoadingSummary] = useState(false);
  const [isLoadingSuggestions, setIsLoadingSuggestions] = useState(false);
  const [isGeneratingReply, setIsGeneratingReply] = useState(false);
  const [isRefreshing, setIsRefreshing] = useState(false);
  const [summaryError, setSummaryError] = useState<string | null>(null);
  const [suggestionsError, setSuggestionsError] = useState<string | null>(null);
  const [generatedReplyError, setGeneratedReplyError] = useState<string | null>(null);
  const summaryRequestIdRef = useRef(0);
  const suggestionsRequestIdRef = useRef(0);
  const generatedRequestIdRef = useRef(0);

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
    const requestId = ++summaryRequestIdRef.current;
    try {
      const response = await api.getConversationSummary(conversationId);
      if (requestId !== summaryRequestIdRef.current) {
        return;
      }

      setSummary(normalizeSummary(response));
    } catch (failure) {
      if (requestId !== summaryRequestIdRef.current) {
        return;
      }

      if (failure instanceof ApiError && failure.status === 401) {
        onUnauthorized();
        return;
      }

      setSummaryError(failure instanceof Error ? failure.message : "Unable to load the conversation summary.");
    } finally {
      if (requestId === summaryRequestIdRef.current) {
        setIsLoadingSummary(false);
      }
    }
  }, [accessToken, api, conversationId, normalizeSummary, onUnauthorized]);

  const loadSuggestions = useCallback(async () => {
    if (!conversationId || !accessToken) {
      setSuggestions([]);
      setSuggestionMetadata(null);
      setSuggestionsError(null);
      setIsLoadingSuggestions(false);
      return;
    }

    setIsLoadingSuggestions(true);
    setSuggestionsError(null);
    const requestId = ++suggestionsRequestIdRef.current;
    try {
      const response: ConversationCopilotSuggestionsResponse = await api.getSuggestedReplies(conversationId, tone);
      if (requestId !== suggestionsRequestIdRef.current) {
        return;
      }

      setSuggestions(Array.isArray(response.suggestedReplies) ? response.suggestedReplies : []);
      setSuggestionMetadata(response);
    } catch (failure) {
      if (requestId !== suggestionsRequestIdRef.current) {
        return;
      }

      if (failure instanceof ApiError && failure.status === 401) {
        onUnauthorized();
        return;
      }

      setSuggestionsError(
        failure instanceof Error
          ? failure.message
          : "Unable to load suggested replies."
      );
    } finally {
      if (requestId === suggestionsRequestIdRef.current) {
        setIsLoadingSuggestions(false);
      }
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

  const generateReply = useCallback(async (request?: CopilotSuggestReplyRequest) => {
    if (!conversationId || !accessToken) {
      return;
    }

    setIsGeneratingReply(true);
    setGeneratedReplyError(null);
    const requestId = ++generatedRequestIdRef.current;

    try {
      const response = await api.suggestReply(conversationId, {
        includeInternalNotes: false,
        maxContextMessages: 12,
        ...request
      });

      if (requestId !== generatedRequestIdRef.current) {
        return;
      }

      setGeneratedReply(response);
      setGeneratedReplyDraft(response.suggestedReply ?? "");
      setGeneratedReplyTone(tone);
    } catch (failure) {
      if (requestId !== generatedRequestIdRef.current) {
        return;
      }

      if (failure instanceof ApiError && failure.status === 401) {
        onUnauthorized();
        return;
      }

      setGeneratedReplyError(
        failure instanceof Error
          ? failure.message
          : "Unable to generate a reply right now."
      );
    } finally {
      if (requestId === generatedRequestIdRef.current) {
        setIsGeneratingReply(false);
      }
    }
  }, [accessToken, api, conversationId, onUnauthorized, tone]);

  const clearGeneratedReply = useCallback(() => {
    setGeneratedReply(null);
    setGeneratedReplyDraft("");
    setGeneratedReplyTone(null);
    setGeneratedReplyError(null);
  }, []);

  useEffect(() => {
    summaryRequestIdRef.current += 1;
    suggestionsRequestIdRef.current += 1;
    generatedRequestIdRef.current += 1;

    if (!conversationId || !accessToken) {
      setSummary(null);
      setSuggestions([]);
      setSuggestionMetadata(null);
      setGeneratedReply(null);
      setGeneratedReplyDraft("");
      setGeneratedReplyTone(null);
      setSummaryError(null);
      setSuggestionsError(null);
      setGeneratedReplyError(null);
      return;
    }

    setGeneratedReply(null);
    setGeneratedReplyDraft("");
    setGeneratedReplyTone(null);
    setGeneratedReplyError(null);

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
    suggestionMetadata,
    generatedReply,
    generatedReplyDraft,
    generatedReplyTone,
    tone,
    isLoadingSummary,
    isLoadingSuggestions,
    isGeneratingReply,
    isRefreshing,
    summaryError,
    suggestionsError,
    generatedReplyError,
    setTone,
    setGeneratedReplyDraft,
    generateReply,
    clearGeneratedReply,
    refreshAll,
    retrySummary: loadSummary,
    retrySuggestions: loadSuggestions
  };
}

export function getCopilotWarningLabel(warning: CopilotContextWarning): string {
  switch (warning) {
    case "MissingProperty":
      return "Property details are incomplete.";
    case "MissingReservation":
      return "Reservation details are unavailable.";
    case "NoApprovedKnowledge":
      return "No approved property knowledge matched this request.";
    case "NoVisibleMessages":
      return "No guest-visible conversation history is available.";
    case "ContextTruncated":
      return "Some older context was omitted.";
    case "AmbiguousGuestRequest":
      return "The guest's request may need clarification.";
    case "ConflictingKnowledge":
      return "Approved property information may contain conflicting details.";
    default:
      return "Additional context may be needed.";
  }
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