import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { createHostConversationsApi } from "../api/hostConversationsApi";
import { ApiError, HttpClient } from "../api/httpClient";
import type { ConversationListQuery, ConversationListResponse } from "../models/hostConversations";
import type { ConversationStatus } from "../models/enums";

interface UseHostConversationsOptions {
  accessToken: string | null;
  onUnauthorized: () => void;
}

export interface UseHostConversationsResult {
  response: ConversationListResponse | null;
  isLoading: boolean;
  error: string | null;
  sessionExpired: boolean;
  selectedConversationId: string | null;
  search: string;
  status?: ConversationStatus;
  requiresHostAttention?: boolean;
  page: number;
  pageSize: number;
  setSearch: (value: string) => void;
  setStatus: (value?: ConversationStatus) => void;
  setRequiresHostAttention: (value?: boolean) => void;
  setPage: (value: number) => void;
  setPageSize: (value: number) => void;
  refresh: () => Promise<void>;
  clearError: () => void;
  selectConversation: (conversationId: string) => void;
}

export function useHostConversations({ accessToken, onUnauthorized }: UseHostConversationsOptions): UseHostConversationsResult {
  const [response, setResponse] = useState<ConversationListResponse | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [sessionExpired, setSessionExpired] = useState(false);
  const [selectedConversationId, setSelectedConversationId] = useState<string | null>(null);

  const [search, setSearch] = useState("");
  const [debouncedSearch, setDebouncedSearch] = useState("");
  const [status, setStatus] = useState<ConversationStatus | undefined>(undefined);
  const [requiresHostAttention, setRequiresHostAttention] = useState<boolean | undefined>(undefined);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const [refreshKey, setRefreshKey] = useState(0);

  const requestVersion = useRef(0);

  const http = useMemo(
    () =>
      new HttpClient({
        baseUrl: import.meta.env.VITE_STAYFLOW_API_URL ?? "http://localhost:5243",
        getAccessToken: () => accessToken
      }),
    [accessToken]
  );

  const api = useMemo(() => createHostConversationsApi(http), [http]);

  useEffect(() => {
    const timer = window.setTimeout(() => {
      setDebouncedSearch(search.trim());
    }, 300);

    return () => window.clearTimeout(timer);
  }, [search]);

  const loadConversations = useCallback(async () => {
    if (!accessToken) {
      setResponse(null);
      setError(null);
      setSessionExpired(false);
      return;
    }

    const version = ++requestVersion.current;
    setIsLoading(true);
    setError(null);

    const query: ConversationListQuery = {
      status,
      propertyId: undefined,
      requiresHostAttention,
      search: debouncedSearch || undefined,
      page,
      pageSize
    };

    try {
      const nextResponse = await api.listConversations(query);
      if (version !== requestVersion.current) {
        return;
      }

      setResponse(nextResponse);
      setSelectedConversationId((current) => {
        const hasSelectedConversation = nextResponse.items.some(
          (conversation) => conversation.conversationId === current
        );

        return hasSelectedConversation ? current : (nextResponse.items[0]?.conversationId ?? null);
      });
    } catch (failure) {
      if (version !== requestVersion.current) {
        return;
      }

      if (failure instanceof ApiError && failure.status === 401) {
        setSessionExpired(true);
        onUnauthorized();
        return;
      }

      const message = failure instanceof Error ? failure.message : "Unable to load conversations.";
      setError(message);
    } finally {
      if (version === requestVersion.current) {
        setIsLoading(false);
      }
    }
  }, [accessToken, api, debouncedSearch, onUnauthorized, page, pageSize, requiresHostAttention, status]);

  useEffect(() => {
    void loadConversations();
  }, [loadConversations, refreshKey]);

  useEffect(() => {
    setPage(1);
  }, [debouncedSearch, status, requiresHostAttention, pageSize]);

  const refresh = useCallback(async () => {
    setRefreshKey((current) => current + 1);
  }, []);

  const setSafePage = useCallback((value: number) => {
    setPage(Math.max(1, value));
  }, []);

  const updatePageSize = useCallback((value: number) => {
    setPageSize(value);
    setPage(1);
  }, []);

  return {
    response,
    isLoading,
    error,
    sessionExpired,
    selectedConversationId,
    search,
    status,
    requiresHostAttention,
    page,
    pageSize,
    setSearch,
    setStatus: (value?: ConversationStatus) => {
      setStatus(value);
      setPage(1);
    },
    setRequiresHostAttention: (value?: boolean) => {
      setRequiresHostAttention(value);
      setPage(1);
    },
    setPage: setSafePage,
    setPageSize: updatePageSize,
    refresh,
    clearError: () => setError(null),
    selectConversation: (conversationId: string) => setSelectedConversationId(conversationId)
  };
}
