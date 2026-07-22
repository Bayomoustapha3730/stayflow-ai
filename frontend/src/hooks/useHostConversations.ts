import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { createHostConversationsApi } from "../api/hostConversationsApi";
import { ApiError, HttpClient } from "../api/httpClient";
import { useConversationRealtime } from "./useConversationRealtime";
import type { ConversationListQuery, ConversationListResponse } from "../models/hostConversations";
import { ConversationMessageType, ConversationSenderType, ConversationStatus, requiresHostAttention as computeRequiresHostAttention } from "../models/enums";

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
  totalUnreadCount: number;
  realtimeState: "offline" | "connecting" | "online" | "reconnecting";
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
  const refreshTimerRef = useRef<number | null>(null);

  const parseRealtimeStatus = useCallback((value?: string): ConversationStatus | undefined => {
    if (!value) {
      return undefined;
    }

    const normalized = value.trim().toLowerCase();
    switch (normalized) {
      case "open":
        return ConversationStatus.Open;
      case "awaitingguest":
        return ConversationStatus.AwaitingGuest;
      case "awaitinghost":
        return ConversationStatus.AwaitingHost;
      case "escalated":
        return ConversationStatus.Escalated;
      case "humanmanaged":
        return ConversationStatus.HumanManaged;
      case "resolved":
        return ConversationStatus.Resolved;
      case "closed":
        return ConversationStatus.Closed;
      default:
        return undefined;
    }
  }, []);

  const scheduleRefresh = useCallback((delayMs = 350) => {
    if (refreshTimerRef.current) {
      window.clearTimeout(refreshTimerRef.current);
    }

    refreshTimerRef.current = window.setTimeout(() => {
      refreshTimerRef.current = null;
      setRefreshKey((current) => current + 1);
    }, delayMs);
  }, []);

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
    if (refreshTimerRef.current) {
      window.clearTimeout(refreshTimerRef.current);
      refreshTimerRef.current = null;
    }

    setRefreshKey((current) => current + 1);
  }, []);

  useEffect(() => {
    return () => {
      if (refreshTimerRef.current) {
        window.clearTimeout(refreshTimerRef.current);
      }
    };
  }, []);

  const realtime = useConversationRealtime({
    accessToken,
    conversationId: null,
    enabled: Boolean(accessToken),
    onMessageCreated: (event) => {
      setResponse((current) => {
        if (!current) {
          return current;
        }

        const index = current.items.findIndex((item) => item.conversationId === event.conversationId);
        if (index < 0) {
          return current;
        }

        const incomingMessage = event.message;
        if (incomingMessage.isInternal || incomingMessage.messageType === ConversationMessageType.InternalNote) {
          return current;
        }

        const updatedItems = [...current.items];
        const existing = updatedItems[index];
        const nextUnreadCount = incomingMessage.senderType === ConversationSenderType.Guest
          ? existing.unreadMessageCount + 1
          : existing.unreadMessageCount;

        updatedItems[index] = {
          ...existing,
          latestVisibleMessagePreview: incomingMessage.content,
          latestVisibleMessageSenderType: incomingMessage.senderType,
          latestVisibleMessageTimestamp: incomingMessage.sentAt,
          unreadMessageCount: nextUnreadCount,
          totalVisibleMessageCount: existing.totalVisibleMessageCount + 1,
          lastActivityAt: incomingMessage.sentAt
        };

        return {
          ...current,
          items: updatedItems,
          totalUnreadCount: incomingMessage.senderType === ConversationSenderType.Guest
            ? current.totalUnreadCount + 1
            : current.totalUnreadCount
        };
      });
    },
    onUnreadChanged: (event) => {
      if (!event.conversationId) {
        scheduleRefresh();
        return;
      }

      scheduleRefresh();
    },
    onAssigned: (event) => {
      setResponse((current) => {
        if (!current) {
          return current;
        }

        const index = current.items.findIndex((item) => item.conversationId === event.conversationId);
        if (index < 0) {
          return current;
        }

        const updatedItems = [...current.items];
        const existing = updatedItems[index];
        const nextStatus = parseRealtimeStatus(event.status) ?? existing.status;

        updatedItems[index] = {
          ...existing,
          assignedUser: event.assignedUser === undefined ? existing.assignedUser : event.assignedUser,
          humanTakeoverEnabled: event.humanTakeoverEnabled ?? existing.humanTakeoverEnabled,
          status: nextStatus,
          requiresHostAttention: computeRequiresHostAttention(nextStatus, event.humanTakeoverEnabled ?? existing.humanTakeoverEnabled),
          lastActivityAt: event.timestamp ?? existing.lastActivityAt
        };

        return {
          ...current,
          items: updatedItems
        };
      });

      scheduleRefresh();
    },
    onReadStateChanged: (event) => {
      setResponse((current) => {
        if (!current) {
          return current;
        }

        if (event.participantKind.toLowerCase() !== "hostuser") {
          return current;
        }

        const index = current.items.findIndex((item) => item.conversationId === event.conversationId);
        if (index < 0) {
          return current;
        }

        const updatedItems = [...current.items];
        const existing = updatedItems[index];
        const totalBefore = current.items.reduce((sum, item) => sum + item.unreadMessageCount, 0);
        updatedItems[index] = {
          ...existing,
          unreadMessageCount: 0,
          lastReadAt: event.lastReadAt ?? existing.lastReadAt
        };
        const totalAfter = updatedItems.reduce((sum, item) => sum + item.unreadMessageCount, 0);

        return {
          ...current,
          items: updatedItems,
          totalUnreadCount: Math.max(0, current.totalUnreadCount - (totalBefore - totalAfter))
        };
      });

      scheduleRefresh();
    },
    onStateChanged: (event) => {
      setResponse((current) => {
        if (!current) {
          return current;
        }

        const index = current.items.findIndex((item) => item.conversationId === event.conversationId);
        if (index < 0) {
          return current;
        }

        const updatedItems = [...current.items];
        const existing = updatedItems[index];
        const nextStatus = parseRealtimeStatus(event.status) ?? existing.status;
        const nextTakeover = event.humanTakeoverEnabled ?? existing.humanTakeoverEnabled;

        updatedItems[index] = {
          ...existing,
          status: nextStatus,
          humanTakeoverEnabled: nextTakeover,
          requiresHostAttention: computeRequiresHostAttention(nextStatus, nextTakeover),
          lastActivityAt: event.timestamp ?? existing.lastActivityAt
        };

        return {
          ...current,
          items: updatedItems
        };
      });

      scheduleRefresh();
    }
  });

  useEffect(() => {
    if (!accessToken) {
      return;
    }

    let timerId: number | null = null;
    let disposed = false;

    const scheduleNext = () => {
      if (disposed) {
        return;
      }

      if (realtime.connectionState === "online") {
        return;
      }

      const isVisible = document.visibilityState === "visible";
      const delayMs = realtime.connectionState === "reconnecting"
        ? (isVisible ? 15000 : 30000)
        : (isVisible ? 12000 : 45000);

      timerId = window.setTimeout(async () => {
        await loadConversations();
        scheduleNext();
      }, delayMs);
    };

    scheduleNext();

    const handleVisibilityChange = () => {
      if (realtime.connectionState !== "online" && document.visibilityState === "visible") {
        void loadConversations();
      }

      if (timerId) {
        window.clearTimeout(timerId);
        timerId = null;
      }

      scheduleNext();
    };

    document.addEventListener("visibilitychange", handleVisibilityChange);

    return () => {
      disposed = true;
      document.removeEventListener("visibilitychange", handleVisibilityChange);
      if (timerId) {
        window.clearTimeout(timerId);
      }
    };
  }, [accessToken, loadConversations, realtime.connectionState]);

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
    totalUnreadCount: response?.totalUnreadCount ?? 0,
    realtimeState: realtime.connectionState,
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
