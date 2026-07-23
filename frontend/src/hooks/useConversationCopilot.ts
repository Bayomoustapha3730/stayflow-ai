import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { ApiError, HttpClient, createHostCopilotApi } from "../api";
import type {
  CopilotSuggestReplyResponse,
  ConversationCopilotSuggestionsResponse,
  ConversationCopilotSummaryResponse,
  CopilotTone,
  CopilotUrgency
} from "../models/copilot";
import { COPILOT_MAX_INSTRUCTION_LENGTH } from "../models/copilot";

interface UseConversationCopilotOptions {
  conversationId: string | null;
  accessToken: string | null;
  onUnauthorized: () => void;
}

export interface UseConversationCopilotResult {
  summary: ConversationCopilotSummaryResponse | null;
  suggestions: string[];
  tone: CopilotTone;
  generatedReply: string;
  generatedReplyState: "idle" | "loading" | "success" | "error";
  generatedReplyMetadata: CopilotSuggestReplyResponse | null;
  isLoadingSummary: boolean;
  isLoadingSuggestions: boolean;
  isGeneratingReply: boolean;
  isRefreshing: boolean;
  summaryError: string | null;
  suggestionsError: string | null;
  generationError: string | null;
  setTone: (tone: CopilotTone) => void;
  refreshAll: () => Promise<void>;
  generateReply: (options?: { guidance?: string; rewriteDraft?: string }) => Promise<boolean>;
  retryGenerateReply: () => Promise<boolean>;
  clearGeneratedReply: () => void;
  retrySummary: () => Promise<void>;
  retrySuggestions: () => Promise<void>;
}

const supportedTones: CopilotTone[] = ["professional", "friendly", "luxury", "casual"];

export function useConversationCopilot({
  conversationId,
  accessToken,
  onUnauthorized
}: UseConversationCopilotOptions): UseConversationCopilotResult {
  const [summary, setSummary] = useState<ConversationCopilotSummaryResponse | null>(null);
  const [suggestions, setSuggestions] = useState<string[]>([]);
  const [tone, setTone] = useState<CopilotTone>("professional");
  const [generatedReply, setGeneratedReply] = useState("");
  const [generatedReplyMetadata, setGeneratedReplyMetadata] = useState<CopilotSuggestReplyResponse | null>(null);
  const [isLoadingSummary, setIsLoadingSummary] = useState(false);
  const [isLoadingSuggestions, setIsLoadingSuggestions] = useState(false);
  const [isGeneratingReply, setIsGeneratingReply] = useState(false);
  const [isRefreshing, setIsRefreshing] = useState(false);
  const [summaryError, setSummaryError] = useState<string | null>(null);
  const [suggestionsError, setSuggestionsError] = useState<string | null>(null);
  const [generationError, setGenerationError] = useState<string | null>(null);

  const http = useMemo(
    () =>
      new HttpClient({
        baseUrl: import.meta.env.VITE_STAYFLOW_API_URL ?? "http://localhost:5243",
        getAccessToken: () => accessToken
      }),
    [accessToken]
  );

  const api = useMemo(() => createHostCopilotApi(http), [http]);
  const summaryRequestIdRef = useRef(0);
  const suggestionsRequestIdRef = useRef(0);
  const generationRequestIdRef = useRef(0);
  const lastGenerationOptionsRef = useRef<{ guidance?: string; rewriteDraft?: string } | null>(null);

  const normalizeTone = useCallback((value: string): CopilotTone => {
    return supportedTones.includes(value as CopilotTone)
      ? (value as CopilotTone)
      : "professional";
  }, []);

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

      setSummaryError(
        failure instanceof Error
          ? failure.message
          : "Unable to load the conversation summary."
      );
    } finally {
      if (requestId === summaryRequestIdRef.current) {
        setIsLoadingSummary(false);
      }
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
    const requestId = ++suggestionsRequestIdRef.current;
    try {
      const response: ConversationCopilotSuggestionsResponse = await api.getSuggestedReplies(conversationId, tone);
      if (requestId !== suggestionsRequestIdRef.current) {
        return;
      }

      setSuggestions(Array.isArray(response.suggestedReplies) ? response.suggestedReplies : []);
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
      setSuggestions([]);
    } finally {
      if (requestId === suggestionsRequestIdRef.current) {
        setIsLoadingSuggestions(false);
      }
    }
  }, [accessToken, api, conversationId, onUnauthorized, tone]);

  const generatedReplyState: "idle" | "loading" | "success" | "error" = isGeneratingReply
    ? "loading"
    : generationError
      ? "error"
      : generatedReply.trim().length > 0
        ? "success"
        : "idle";

  const generateReply = useCallback(async (options?: { guidance?: string; rewriteDraft?: string }) => {
    if (!conversationId || !accessToken || isGeneratingReply) {
      return false;
    }

    const effectiveOptions = options ?? lastGenerationOptionsRef.current ?? {};

    const draftToRewrite = effectiveOptions?.rewriteDraft?.trim() ?? "";
    const guidance = effectiveOptions?.guidance?.trim() ?? "";
    if (guidance.length > COPILOT_MAX_INSTRUCTION_LENGTH) {
      setGenerationError(`Instruction must be ${COPILOT_MAX_INSTRUCTION_LENGTH} characters or fewer.`);
      return false;
    }

    const finalGuidance = draftToRewrite.length > 0
      ? `${guidance ? `${guidance}\n\n` : ""}Rewrite this draft while keeping factual accuracy and guest-safe wording:\n${draftToRewrite}`
      : guidance;

    setIsGeneratingReply(true);
    setGenerationError(null);
    const requestId = ++generationRequestIdRef.current;
    lastGenerationOptionsRef.current = {
      guidance,
      rewriteDraft: draftToRewrite || undefined
    };

    try {
      const response = await api.generateReply(conversationId, {
        guidance: finalGuidance || undefined,
        tone,
        includeInternalNotes: false,
        maxContextMessages: 12
      });

      if (requestId !== generationRequestIdRef.current) {
        return false;
      }

      setGeneratedReply(response.suggestedReply ?? "");
      setGeneratedReplyMetadata(response);
      return true;
    } catch (failure) {
      if (failure instanceof ApiError && failure.status === 401) {
        onUnauthorized();
        return false;
      }

      if (requestId !== generationRequestIdRef.current) {
        return false;
      }

      if (failure instanceof ApiError) {
        setGenerationError(mapGenerateReplyError(failure));
      } else {
        setGenerationError("Unable to generate a reply.");
      }
      return false;
    } finally {
      if (requestId === generationRequestIdRef.current) {
        setIsGeneratingReply(false);
      }
    }
  }, [accessToken, api, conversationId, isGeneratingReply, onUnauthorized, tone]);

  const clearGeneratedReply = useCallback(() => {
    setGeneratedReply("");
    setGeneratedReplyMetadata(null);
    setGenerationError(null);
    lastGenerationOptionsRef.current = null;
  }, []);

  const retryGenerateReply = useCallback(async () => {
    return generateReply();
  }, [generateReply]);

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
      setGeneratedReply("");
      setGeneratedReplyMetadata(null);
      setSummaryError(null);
      setSuggestionsError(null);
      setGenerationError(null);
      return;
    }

    clearGeneratedReply();
    generationRequestIdRef.current += 1;
    void loadSummary();
  }, [accessToken, clearGeneratedReply, conversationId, loadSummary]);

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
    generatedReply,
    generatedReplyState,
    generatedReplyMetadata,
    isLoadingSummary,
    isLoadingSuggestions,
    isGeneratingReply,
    isRefreshing,
    summaryError,
    suggestionsError,
    generationError,
    setTone: (value) => setTone(normalizeTone(value)),
    refreshAll,
    generateReply,
    retryGenerateReply,
    clearGeneratedReply,
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

function mapGenerateReplyError(error: ApiError): string {
  if (error.status === 400) {
    return error.errors[0] || error.message || "Unable to generate a reply.";
  }

  if (error.status === 403) {
    return "You do not have permission to generate replies.";
  }

  if (error.status === 404) {
    return "Conversation unavailable.";
  }

  if (error.status === 0 || error.status === 408) {
    return "Unable to reach StayFlow.";
  }

  return "Unable to generate a reply.";
}