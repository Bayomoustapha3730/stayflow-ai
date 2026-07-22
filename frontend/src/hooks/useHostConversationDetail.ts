import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { createHostConversationsApi } from "../api/hostConversationsApi";
import { ApiError, HttpClient } from "../api/httpClient";
import type { ConversationDetail, ConversationMessage } from "../models/hostConversations";

const pollIntervalMs = 10000;

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
  refresh: () => Promise<void>;
  sendHostMessage: (content: string) => Promise<boolean>;
  addInternalNote: (content: string) => Promise<boolean>;
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

  useEffect(() => {
    if (!conversationId || !accessToken) {
      return;
    }

    const intervalId = window.setInterval(() => {
      if (document.visibilityState === "hidden") {
        return;
      }

      void loadDetailAndHistory("refresh");
    }, pollIntervalMs);

    return () => {
      window.clearInterval(intervalId);
    };
  }, [accessToken, conversationId, loadDetailAndHistory]);

  const refresh = useCallback(async () => {
    await loadDetailAndHistory("refresh");
  }, [loadDetailAndHistory]);

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

      return runConversationAction(
        () => api.addHostMessage(conversationId as string, trimmed),
        setIsSendingReply
      );
    },
    [api, conversationId, isSendingReply, runConversationAction]
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

      return runConversationAction(
        () => api.addInternalNote(conversationId as string, trimmed),
        setIsAddingNote
      );
    },
    [api, conversationId, isAddingNote, runConversationAction]
  );

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
    refresh,
    sendHostMessage,
    addInternalNote,
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
