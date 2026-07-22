import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { createHostConversationsApi } from "../api/hostConversationsApi";
import { ApiError, HttpClient } from "../api/httpClient";
import { useConversationRealtime } from "./useConversationRealtime";
import { ConversationMessageType, ConversationSenderType, ConversationStatus } from "../models/enums";
import type { ConversationDetail, ConversationMessage } from "../models/hostConversations";

const maxMessageLength = 2000;

interface UseHostConversationDetailOptions {
  conversationId: string | null;
  accessToken: string | null;
  onUnauthorized: () => void;
  onConversationChanged?: () => void;
}

export interface UseHostConversationDetailResult {
  conversation: ConversationDetail | null;
  messages: ConversationMessage[];
  isLoading: boolean;
  isRefreshing: boolean;
  isSendingReply: boolean;
  isAddingNote: boolean;
  isChangingMode: boolean;
  isResolving: boolean;
  isClosing: boolean;
  error: string | null;
  actionError: string | null;
  realtimeState: "offline" | "connecting" | "online" | "reconnecting";
  isGuestTyping: boolean;
  isAnotherStaffTyping: boolean;
  isInternalNoteTyping: boolean;
  refresh: () => Promise<void>;
  sendHostMessage: (content: string) => Promise<boolean>;
  addInternalNote: (content: string) => Promise<boolean>;
  retryFailedMessage: (messageId: string) => Promise<boolean>;
  assignToMe: () => Promise<boolean>;
  unassign: () => Promise<boolean>;
  startTyping: (context: "host" | "internal-note") => Promise<void>;
  stopTyping: (context: "host" | "internal-note") => Promise<void>;
  enableHumanTakeover: () => Promise<boolean>;
  returnToAI: () => Promise<boolean>;
  resolveConversation: () => Promise<boolean>;
  closeConversation: () => Promise<boolean>;
  clearError: () => void;
}

function sortMessagesChronologically(messages: ConversationMessage[]): ConversationMessage[] {
  return [...messages].sort((left, right) => {
    const leftTime = Date.parse(left.sentAt);
    const rightTime = Date.parse(right.sentAt);

    if (leftTime === rightTime) {
      return left.id.localeCompare(right.id);
    }

    return leftTime - rightTime;
  });
}

export function useHostConversationDetail({
  conversationId,
  accessToken,
  onUnauthorized,
  onConversationChanged
}: UseHostConversationDetailOptions): UseHostConversationDetailResult {
  const [conversation, setConversation] = useState<ConversationDetail | null>(null);
  const [messages, setMessages] = useState<ConversationMessage[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [isRefreshing, setIsRefreshing] = useState(false);
  const [isSendingReply, setIsSendingReply] = useState(false);
  const [isAddingNote, setIsAddingNote] = useState(false);
  const [isChangingMode, setIsChangingMode] = useState(false);
  const [isResolving, setIsResolving] = useState(false);
  const [isClosing, setIsClosing] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const [isGuestTyping, setIsGuestTyping] = useState(false);
  const [isAnotherStaffTyping, setIsAnotherStaffTyping] = useState(false);
  const [isInternalNoteTyping, setIsInternalNoteTyping] = useState(false);

  const requestVersion = useRef(0);
  const lastReadMarkAt = useRef(0);

  const http = useMemo(
    () =>
      new HttpClient({
        baseUrl: import.meta.env.VITE_STAYFLOW_API_URL ?? "http://localhost:5243",
        getAccessToken: () => accessToken
      }),
    [accessToken]
  );

  const api = useMemo(() => createHostConversationsApi(http), [http]);

  const currentUserId = useMemo(() => {
    if (!accessToken) {
      return null;
    }

    try {
      const [, payload] = accessToken.split(".");
      const parsed = JSON.parse(atob(payload.replace(/-/g, "+").replace(/_/g, "/")));
      return typeof parsed.sub === "string" ? parsed.sub : null;
    } catch {
      return null;
    }
  }, [accessToken]);

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

  const loadDetailAndHistory = useCallback(
    async (reason: "initial" | "refresh") => {
      if (!conversationId || !accessToken) {
        setConversation(null);
        setMessages([]);
        setError(null);
        setActionError(null);
        setIsLoading(false);
        setIsRefreshing(false);
        return false;
      }

      const version = ++requestVersion.current;

      if (reason === "initial") {
        setIsLoading(true);
      } else {
        setIsRefreshing(true);
      }
      setError(null);

      try {
        const [detail, history] = await Promise.all([
          api.getConversation(conversationId),
          api.getMessages(conversationId, {
            includeInternal: true,
            pageNumber: 1,
            pageSize: 100
          })
        ]);

        if (version !== requestVersion.current) {
          return false;
        }

        const sorted = sortMessagesChronologically(history.messages.items);
        setConversation(detail);
        setMessages(sorted);
        if (document.visibilityState === "visible") {
          void api.markRead(conversationId).catch(() => {});
        }
        return true;
      } catch (failure) {
        if (version !== requestVersion.current) {
          return false;
        }

        if (failure instanceof ApiError && failure.status === 401) {
          onUnauthorized();
          return false;
        }

        const message = failure instanceof Error ? failure.message : "Unable to load this conversation.";
        setError(message);
        return false;
      } finally {
        if (version === requestVersion.current) {
          if (reason === "initial") {
            setIsLoading(false);
          } else {
            setIsRefreshing(false);
          }
        }

      }
    },
    [accessToken, api, conversationId, onUnauthorized]
  );

  useEffect(() => {
    if (!conversationId || !accessToken) {
      requestVersion.current += 1;
      setConversation(null);
      setMessages([]);
      setError(null);
      setActionError(null);
      setIsLoading(false);
      setIsRefreshing(false);
      return;
    }

    void loadDetailAndHistory("initial");

    return () => {
      requestVersion.current += 1;
    };
  }, [accessToken, conversationId, loadDetailAndHistory]);

  const refresh = useCallback(async () => {
    await loadDetailAndHistory("refresh");
  }, [loadDetailAndHistory]);

  const markReadIfVisible = useCallback(() => {
    if (!conversationId || !accessToken || document.visibilityState !== "visible") {
      return;
    }

    const now = Date.now();
    if (now - lastReadMarkAt.current < 1500) {
      return;
    }

    lastReadMarkAt.current = now;
    void api.markRead(conversationId)
      .then(() => {
        onConversationChanged?.();
      })
      .catch(() => {
        // Polling + realtime will eventually reconcile read state.
      });
  }, [accessToken, api, conversationId, onConversationChanged]);

  const runConversationAction = useCallback(
    async (
      runRequest: () => Promise<unknown>,
      setBusy: (value: boolean) => void,
      shouldNotifyInbox = true
    ): Promise<boolean> => {
      if (!conversationId || !accessToken) {
        return false;
      }

      setActionError(null);
      setBusy(true);

      try {
        await runRequest();
        await loadDetailAndHistory("refresh");

        if (shouldNotifyInbox) {
          onConversationChanged?.();
        }

        return true;
      } catch (failure) {
        if (failure instanceof ApiError && failure.status === 401) {
          onUnauthorized();
          return false;
        }

        const message = failure instanceof Error ? failure.message : "The action could not be completed.";
        setActionError(message);
        return false;
      } finally {
        setBusy(false);
      }
    },
    [accessToken, conversationId, loadDetailAndHistory, onConversationChanged, onUnauthorized]
  );

  const sendHostMessage = useCallback(
    async (content: string) => {
      if (isSendingReply) {
        return false;
      }

      const trimmed = content.trim();
      if (!trimmed) {
        setActionError("Enter a message before sending.");
        return false;
      }

      if (trimmed.length > maxMessageLength) {
        setActionError(`Content must be ${maxMessageLength} characters or fewer.`);
        return false;
      }

      const optimisticId = `optimistic-host-${crypto.randomUUID()}`;
      const optimisticMessage: ConversationMessage = {
        id: optimisticId,
        conversationId: conversationId as string,
        senderType: ConversationSenderType.Host,
        messageType: ConversationMessageType.Text,
        content: trimmed,
        isInternal: false,
        sentAt: new Date().toISOString(),
        deliveryStatus: "sending",
        optimisticId
      };

      setActionError(null);
      setMessages((current) => sortMessagesChronologically([...current, optimisticMessage]));
      setIsSendingReply(true);

      try {
        const response = await api.addHostMessage(conversationId as string, trimmed);
        setMessages((current) =>
          sortMessagesChronologically(
            current
              .filter((message) => message.id !== response.id)
              .map((message) => (message.id === optimisticId ? response : message))
          )
        );
        onConversationChanged?.();
        markReadIfVisible();
        return true;
      } catch (failure) {
        if (failure instanceof ApiError && failure.status === 401) {
          onUnauthorized();
          return false;
        }

        const message = failure instanceof Error ? failure.message : "The action could not be completed.";
        setActionError(message);
        setMessages((current) =>
          current.map((item) =>
            item.id === optimisticId
              ? {
                  ...item,
                  deliveryStatus: "failed"
                }
              : item
          )
        );
        return false;
      } finally {
        setIsSendingReply(false);
      }
    },
    [accessToken, api, conversationId, isSendingReply, markReadIfVisible, onConversationChanged, onUnauthorized]
  );

  const addInternalNote = useCallback(
    async (content: string) => {
      if (isAddingNote) {
        return false;
      }

      const trimmed = content.trim();
      if (!trimmed) {
        setActionError("Enter a note before submitting.");
        return false;
      }

      if (trimmed.length > maxMessageLength) {
        setActionError(`Content must be ${maxMessageLength} characters or fewer.`);
        return false;
      }

      const optimisticId = `optimistic-note-${crypto.randomUUID()}`;
      const optimisticMessage: ConversationMessage = {
        id: optimisticId,
        conversationId: conversationId as string,
        senderType: ConversationSenderType.System,
        messageType: ConversationMessageType.InternalNote,
        content: trimmed,
        isInternal: true,
        sentAt: new Date().toISOString(),
        authorDisplayName: conversation?.assignedUser?.fullName ?? null,
        deliveryStatus: "sending",
        optimisticId
      };

      setActionError(null);
      setMessages((current) => sortMessagesChronologically([...current, optimisticMessage]));
      setIsAddingNote(true);

      try {
        const response = await api.addInternalNote(conversationId as string, trimmed);
        setMessages((current) =>
          sortMessagesChronologically(
            current
              .filter((message) => message.id !== response.id)
              .map((message) => (message.id === optimisticId ? response : message))
          )
        );
        onConversationChanged?.();
        return true;
      } catch (failure) {
        if (failure instanceof ApiError && failure.status === 401) {
          onUnauthorized();
          return false;
        }

        const message = failure instanceof Error ? failure.message : "The action could not be completed.";
        setActionError(message);
        setMessages((current) =>
          current.map((item) =>
            item.id === optimisticId
              ? {
                  ...item,
                  deliveryStatus: "failed"
                }
              : item
          )
        );
        return false;
      } finally {
        setIsAddingNote(false);
      }
    },
    [api, conversation?.assignedUser?.fullName, conversationId, isAddingNote, onConversationChanged, onUnauthorized]
  );

  const retryFailedMessage = useCallback(
    async (messageId: string) => {
      const failed = messages.find((message) => message.id === messageId && message.deliveryStatus === "failed");
      if (!failed) {
        return false;
      }

      if (failed.isInternal || failed.messageType === ConversationMessageType.InternalNote) {
        return addInternalNote(failed.content);
      }

      return sendHostMessage(failed.content);
    },
    [addInternalNote, messages, sendHostMessage]
  );

  const assignToMe = useCallback(async () => {
    if (isChangingMode) {
      return false;
    }

    return runConversationAction(() => api.assignToMe(conversationId as string), setIsChangingMode);
  }, [api, conversationId, isChangingMode, runConversationAction]);

  const unassign = useCallback(async () => {
    if (isChangingMode) {
      return false;
    }

    return runConversationAction(() => api.unassign(conversationId as string), setIsChangingMode);
  }, [api, conversationId, isChangingMode, runConversationAction]);

  const enableHumanTakeover = useCallback(async () => {
    if (isChangingMode) {
      return false;
    }

    return runConversationAction(
      () => api.enableHumanTakeover(conversationId as string),
      setIsChangingMode
    );
  }, [api, conversationId, isChangingMode, runConversationAction]);

  const returnToAI = useCallback(async () => {
    if (isChangingMode) {
      return false;
    }

    return runConversationAction(() => api.returnToAI(conversationId as string), setIsChangingMode);
  }, [api, conversationId, isChangingMode, runConversationAction]);

  const resolveConversation = useCallback(async () => {
    if (isResolving) {
      return false;
    }

    return runConversationAction(
      () => api.resolveConversation(conversationId as string),
      setIsResolving
    );
  }, [api, conversationId, isResolving, runConversationAction]);

  const closeConversation = useCallback(async () => {
    if (isClosing) {
      return false;
    }

    return runConversationAction(
      () => api.closeConversation(conversationId as string),
      setIsClosing
    );
  }, [api, conversationId, isClosing, runConversationAction]);

  const realtime = useConversationRealtime({
    accessToken,
    conversationId,
    enabled: Boolean(accessToken),
    onMessageCreated: (event) => {
      if (event.conversationId !== conversationId) {
        return;
      }

      setMessages((current) => {
        if (current.some((message) => message.id === event.message.id)) {
          return current;
        }

        const optimisticMatch = current.find((message) =>
          message.deliveryStatus === "sending"
          && message.content === event.message.content
          && message.senderType === event.message.senderType
        );

        if (optimisticMatch) {
          return sortMessagesChronologically(
            current.map((message) => (message.id === optimisticMatch.id ? event.message : message))
          );
        }

        return sortMessagesChronologically([...current, event.message]);
      });

      markReadIfVisible();
      onConversationChanged?.();
    },
    onTypingStarted: (event) => {
      if (event.conversationId !== conversationId) {
        return;
      }

      if (event.context === "guest") {
        setIsGuestTyping(true);
      }

      if (event.context === "host" && event.actorUserId && event.actorUserId !== currentUserId) {
        setIsAnotherStaffTyping(true);
      }

      if (event.context === "internal-note" && event.actorUserId && event.actorUserId !== currentUserId) {
        setIsInternalNoteTyping(true);
      }
    },
    onTypingStopped: (event) => {
      if (event.conversationId !== conversationId) {
        return;
      }

      if (event.context === "guest") {
        setIsGuestTyping(false);
      }

      if (event.context === "host") {
        setIsAnotherStaffTyping(false);
      }

      if (event.context === "internal-note") {
        setIsInternalNoteTyping(false);
      }
    },
    onUnreadChanged: () => {
      onConversationChanged?.();
      markReadIfVisible();
    },
    onAssigned: () => {
      void refresh();
      onConversationChanged?.();
    },
    onReadStateChanged: () => {
      onConversationChanged?.();
    },
    onStateChanged: (event) => {
      if (event.conversationId !== conversationId) {
        return;
      }

      const nextStatus = parseRealtimeStatus(event.status);

      setConversation((current) => {
        if (!current) {
          return current;
        }

        return {
          ...current,
          status: nextStatus ?? current.status,
          humanTakeoverEnabled: event.humanTakeoverEnabled ?? current.humanTakeoverEnabled,
          lastActivityAt: event.timestamp ?? current.lastActivityAt
        };
      });

      onConversationChanged?.();
    }
  });

  useEffect(() => {
    if (!conversationId || !accessToken) {
      return;
    }

    let disposed = false;
    let timerId: number | null = null;

    const computeDelayMs = (): number | null => {
      if (realtime.connectionState === "online") {
        return null;
      }

      const isVisible = document.visibilityState === "visible";
      if (realtime.connectionState === "reconnecting") {
        return isVisible ? 15000 : 30000;
      }

      if (realtime.connectionState === "connecting") {
        return isVisible ? 12000 : 30000;
      }

      return isVisible ? 12000 : 45000;
    };

    const scheduleNextPoll = () => {
      if (disposed) {
        return;
      }

      const delayMs = computeDelayMs();
      if (delayMs === null) {
        return;
      }

      timerId = window.setTimeout(async () => {
        await loadDetailAndHistory("refresh");
        scheduleNextPoll();
      }, delayMs);
    };

    scheduleNextPoll();

    const handleVisibilityChange = () => {
      if (timerId) {
        window.clearTimeout(timerId);
        timerId = null;
      }

      if (document.visibilityState === "visible") {
        void loadDetailAndHistory("refresh");
      }

      scheduleNextPoll();
    };

    document.addEventListener("visibilitychange", handleVisibilityChange);

    return () => {
      disposed = true;
      document.removeEventListener("visibilitychange", handleVisibilityChange);
      if (timerId) {
        window.clearTimeout(timerId);
      }
    };
  }, [accessToken, conversationId, loadDetailAndHistory, realtime.connectionState]);

  useEffect(() => {
    if (!conversationId) {
      return;
    }

    const onVisibilityChange = () => {
      if (document.visibilityState === "visible") {
        markReadIfVisible();
      }
    };

    window.addEventListener("focus", onVisibilityChange);
    document.addEventListener("visibilitychange", onVisibilityChange);

    return () => {
      window.removeEventListener("focus", onVisibilityChange);
      document.removeEventListener("visibilitychange", onVisibilityChange);
    };
  }, [conversationId, markReadIfVisible]);

  useEffect(() => {
    if (messages.length > 0) {
      markReadIfVisible();
    }
  }, [markReadIfVisible, messages.length]);

  return {
    conversation,
    messages,
    isLoading,
    isRefreshing,
    isSendingReply,
    isAddingNote,
    isChangingMode,
    isResolving,
    isClosing,
    error,
    actionError,
    realtimeState: realtime.connectionState,
    isGuestTyping,
    isAnotherStaffTyping,
    isInternalNoteTyping,
    refresh,
    sendHostMessage,
    addInternalNote,
    retryFailedMessage,
    assignToMe,
    unassign,
    startTyping: realtime.startTyping,
    stopTyping: realtime.stopTyping,
    enableHumanTakeover,
    returnToAI,
    resolveConversation,
    closeConversation,
    clearError: () => {
      setError(null);
      setActionError(null);
    }
  };
}
