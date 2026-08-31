import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { createAuthApi, createChatApi, HttpClient, ApiError } from "../api";
import { useConversationRealtime } from "./useConversationRealtime";
import type {
  ConversationMessageFeedbackValue,
  ChatConversation,
  ChatMessage,
  PendingActionCard,
  ChatStatusResponse,
  SendChatMessageRequest
} from "../models/chat";
import { ConversationSenderType, ConversationStatus, GuestChannel, requiresHostAttention } from "../models/enums";
import { buildLocalMessage, mergeMessages } from "../utils/messages";

const tokenStorageKey = "stayflow.demo.accessToken";
const conversationStorageKeyPrefix = "stayflow.chat.conversationId";
const legacyConversationStorageKey = "stayflow.chat.conversationId";
const openStorageKey = "stayflow.chat.isOpen";

export interface ChatConversationScope {
  apiBaseUrl: string;
  guestId?: string;
  reservationId?: string;
  propertyId?: string;
}

function normalizeScopePart(value: string | undefined): string {
  const normalized = (value ?? "").trim().toLowerCase();
  return normalized.length === 0 ? "-" : normalized;
}

/**
 * A stored conversation belongs to one tenant's widget context, so the key is namespaced by
 * every stable identifier the widget has. It is a client-side continuity hint only; the server
 * still authorizes the conversation against the caller's tenant.
 */
export function buildConversationStorageKey(scope: ChatConversationScope): string {
  const parts = [scope.apiBaseUrl, scope.propertyId, scope.reservationId, scope.guestId].map(normalizeScopePart);
  return `${conversationStorageKeyPrefix}::${parts.join("|")}`;
}

function discardLegacyConversationId(): void {
  // Pre-namespacing values cannot be attributed to a tenant context, so they are dropped.
  sessionStorage.removeItem(legacyConversationStorageKey);
}

export interface UseChatOptions {
  apiBaseUrl: string;
  guestId?: string;
  reservationId?: string;
  propertyId?: string;
  channelIdentity?: string;
}

export interface UseChatResult {
  isOpen: boolean;
  isAuthenticated: boolean;
  isLoadingHistory: boolean;
  isSending: boolean;
  isEscalating: boolean;
  isEnding: boolean;
  feedbackSubmittingMessageId: string | null;
  error: string | null;
  unreadCount: number;
  conversationId: string | null;
  conversationStatus: ConversationStatus | null;
  humanTakeoverEnabled: boolean;
  requiresHostAttention: boolean;
  statusMessage: string | null;
  pendingAction: PendingActionCard | null;
  messages: ChatMessage[];
  login: (email: string, password: string) => Promise<void>;
  logout: () => void;
  open: () => void;
  close: () => void;
  toggle: () => void;
  sendMessage: (message: string) => Promise<boolean>;
  loadHistory: () => Promise<void>;
  escalate: (reason?: string) => Promise<void>;
  endConversation: () => Promise<void>;
  submitMessageFeedback: (messageId: string, feedbackValue: ConversationMessageFeedbackValue) => Promise<void>;
  confirmPendingAction: () => Promise<void>;
  cancelPendingAction: () => Promise<void>;
  startNewConversation: () => void;
  clearError: () => void;
  isHostTyping: boolean;
  realtimeState: "offline" | "connecting" | "online" | "reconnecting";
  startTyping: () => Promise<void>;
  stopTyping: () => Promise<void>;
}

export function useChat(options: UseChatOptions): UseChatResult {
  const conversationStorageKey = useMemo(
    () => buildConversationStorageKey({
      apiBaseUrl: options.apiBaseUrl,
      guestId: options.guestId,
      reservationId: options.reservationId,
      propertyId: options.propertyId
    }),
    [options.apiBaseUrl, options.guestId, options.propertyId, options.reservationId]
  );
  const [accessToken, setAccessToken] = useState<string | null>(() => sessionStorage.getItem(tokenStorageKey));
  const [conversationId, setConversationId] = useState<string | null>(() => {
    discardLegacyConversationId();
    return sessionStorage.getItem(conversationStorageKey);
  });
  const [conversationStatus, setConversationStatus] = useState<ConversationStatus | null>(null);
  const [humanTakeoverEnabled, setHumanTakeoverEnabled] = useState(false);
  const [requiresAttention, setRequiresAttention] = useState(false);
  const [statusMessage, setStatusMessage] = useState<string | null>(null);
  const [pendingAction, setPendingAction] = useState<PendingActionCard | null>(null);
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const pendingActionExecutionRef = useRef<string | null>(null);
  const [isOpen, setIsOpen] = useState(() => sessionStorage.getItem(openStorageKey) === "true");
  const [isLoadingHistory, setIsLoadingHistory] = useState(false);
  const [isSending, setIsSending] = useState(false);
  const [isEscalating, setIsEscalating] = useState(false);
  const [isEnding, setIsEnding] = useState(false);
  const [feedbackSubmittingMessageId, setFeedbackSubmittingMessageId] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [unreadCount, setUnreadCount] = useState(0);
  const [isHostTyping, setIsHostTyping] = useState(false);
  const conversationScopeRef = useRef(conversationStorageKey);

  useEffect(() => {
    if (conversationScopeRef.current === conversationStorageKey) {
      return;
    }

    // A new widget context must never inherit the previous context's conversation or transcript.
    conversationScopeRef.current = conversationStorageKey;
    setConversationId(sessionStorage.getItem(conversationStorageKey));
    setConversationStatus(null);
    setHumanTakeoverEnabled(false);
    setRequiresAttention(false);
    setPendingAction(null);
    setMessages([]);
    setUnreadCount(0);
    setError(null);
  }, [conversationStorageKey]);

  const http = useMemo(
    () =>
      new HttpClient({
        baseUrl: options.apiBaseUrl,
        getAccessToken: () => accessToken
      }),
    [accessToken, options.apiBaseUrl]
  );

  const chatApi = useMemo(() => createChatApi(http), [http]);
  const authApi = useMemo(() => createAuthApi(http), [http]);

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

  const markReadIfVisible = useCallback(() => {
    if (!conversationId || !options.guestId || !isOpen || document.visibilityState === "hidden") {
      return;
    }

    void chatApi.markConversationRead(conversationId, options.guestId).catch(() => {
      // Polling/realtime will reconcile eventually.
    });
  }, [chatApi, conversationId, isOpen, options.guestId]);

  const clearSession = useCallback(() => {
    sessionStorage.removeItem(tokenStorageKey);
    sessionStorage.removeItem(conversationStorageKey);
    setAccessToken(null);
    setConversationId(null);
    setConversationStatus(null);
    setHumanTakeoverEnabled(false);
    setRequiresAttention(false);
    setStatusMessage(null);
    setPendingAction(null);
    setMessages([]);
  }, [conversationStorageKey]);

  const handleError = useCallback(
    (failure: unknown) => {
      const message = failure instanceof Error ? failure.message : "Something went wrong. Please try again.";
      if (failure instanceof ApiError && failure.status === 401) {
        clearSession();
      }

      setError(message);
    },
    [clearSession]
  );

  const updateConversationState = useCallback((conversation: ChatConversation | ChatStatusResponse) => {
    setConversationId(conversation.conversationId);
    sessionStorage.setItem(conversationStorageKey, conversation.conversationId);
    setConversationStatus(conversation.status);
    setHumanTakeoverEnabled(conversation.humanTakeoverEnabled);
    setRequiresAttention(
      conversation.requiresHostAttention || requiresHostAttention(conversation.status, conversation.humanTakeoverEnabled)
    );

    const nextStatusMessage = "guestSafeMessage" in conversation ? conversation.guestSafeMessage : null;
    if (nextStatusMessage) {
      setStatusMessage(nextStatusMessage);
    }
  }, [conversationStorageKey]);

  const login = useCallback(
    async (email: string, password: string) => {
      setError(null);
      try {
        const auth = await authApi.loginForDevelopment(email.trim(), password);
        setAccessToken(auth.accessToken);
        sessionStorage.setItem(tokenStorageKey, auth.accessToken);
      } catch (failure) {
        handleError(failure);
        throw failure;
      }
    },
    [authApi, handleError]
  );

  const logout = useCallback(() => {
    clearSession();
    setError(null);
  }, [clearSession]);

  const open = useCallback(() => {
    sessionStorage.setItem(openStorageKey, "true");
    setIsOpen(true);
    setUnreadCount(0);
    markReadIfVisible();
  }, []);

  const close = useCallback(() => {
    sessionStorage.setItem(openStorageKey, "false");
    setIsOpen(false);
  }, []);

  const toggle = useCallback(() => {
    setIsOpen((current) => {
      if (!current) {
        setUnreadCount(0);
        markReadIfVisible();
      }

      sessionStorage.setItem(openStorageKey, String(!current));
      return !current;
    });
  }, [markReadIfVisible]);

  const loadHistory = useCallback(async () => {
    if (!conversationId) {
      return;
    }

    setIsLoadingHistory(true);
    setError(null);

    try {
      const conversation = await chatApi.getChatConversation(conversationId);
      updateConversationState(conversation);
      setMessages((current) => mergeMessages(current, conversation.recentMessages));

      const history = await chatApi.getChatHistory(conversationId, 1, 50);
      setMessages((current) => mergeMessages(current, history.messages.items));
      markReadIfVisible();
    } catch (failure) {
      handleError(failure);
    } finally {
      setIsLoadingHistory(false);
    }
  }, [chatApi, conversationId, handleError, markReadIfVisible, updateConversationState]);

  const sendMessage = useCallback(
    async (message: string): Promise<boolean> => {
      const trimmed = message.trim();
      if (!trimmed || !options.guestId || isSending || conversationStatus === ConversationStatus.Closed) {
        return false;
      }

      const optimisticMessage = buildLocalMessage(trimmed, conversationId ?? undefined);
      setIsSending(true);
      setError(null);
      setMessages((current) => mergeMessages(current, [optimisticMessage]));

      const request: SendChatMessageRequest = {
        conversationId: conversationId ?? undefined,
        guestId: options.guestId,
        reservationId: options.reservationId,
        propertyId: options.propertyId,
        message: trimmed,
        channel: GuestChannel.Web,
        channelIdentity: options.channelIdentity,
        externalMessageId: crypto.randomUUID(),
        currentTimestamp: new Date().toISOString()
      };

      try {
        const response = await chatApi.sendChatMessage(request);
        setConversationId(response.conversationId);
        sessionStorage.setItem(conversationStorageKey, response.conversationId);
        setConversationStatus(response.conversationStatus);
        setHumanTakeoverEnabled(response.humanTakeoverEnabled);
        setRequiresAttention(
          response.requiresHostAttention ||
            requiresHostAttention(response.conversationStatus, response.humanTakeoverEnabled)
        );
        const nextStatusMessage = response.assistantMessage?.content ?? null;
        if (response.requiresHostAttention || response.humanTakeoverEnabled || requiresHostAttention(response.conversationStatus, response.humanTakeoverEnabled)) {
          setStatusMessage(nextStatusMessage ?? "A host has been notified and will respond as soon as possible.");
        } else {
          setStatusMessage(null);
        }
        setPendingAction(response.pendingAction ?? null);

        const returnedMessages = [response.guestMessage, response.assistantMessage].filter(Boolean) as ChatMessage[];
        setMessages((current) =>
          mergeMessages(
            current.filter((item) => item.id !== optimisticMessage.id),
            returnedMessages
          )
        );

        if (!isOpen && response.assistantMessage) {
          setUnreadCount((current) => current + 1);
        }

        markReadIfVisible();

        return true;
      } catch (failure) {
        setMessages((current) =>
          current.map((item) => (item.id === optimisticMessage.id ? { ...item, localStatus: "failed" } : item))
        );
        handleError(failure);
        return false;
      } finally {
        setIsSending(false);
      }
    },
    [
      chatApi,
      conversationId,
      conversationStatus,
      conversationStorageKey,
      handleError,
      isOpen,
      isSending,
      options.channelIdentity,
      options.guestId,
      options.propertyId,
      options.reservationId
    ]
  );

  const realtime = useConversationRealtime({
    accessToken,
    conversationId,
    enabled: Boolean(accessToken),
    onMessageCreated: (event) => {
      if (event.conversationId !== conversationId || event.message.isInternal) {
        return;
      }

      const incoming = event.message as ChatMessage;
      setMessages((current) => mergeMessages(current, [incoming]));

      if (incoming.senderType === ConversationSenderType.Host) {
        if (!isOpen || document.visibilityState === "hidden") {
          setUnreadCount((current) => current + 1);
        } else {
          markReadIfVisible();
        }
      }
    },
    onTypingStarted: (event) => {
      if (event.conversationId === conversationId && event.context === "host") {
        setIsHostTyping(true);
      }
    },
    onTypingStopped: (event) => {
      if (event.conversationId === conversationId && event.context === "host") {
        setIsHostTyping(false);
      }
    },
    onStateChanged: (event) => {
      if (event.conversationId !== conversationId) {
        return;
      }

      const nextStatus = parseRealtimeStatus(event.status);
      if (nextStatus !== undefined) {
        setConversationStatus(nextStatus);
      }

      const nextTakeover = event.humanTakeoverEnabled;
      if (nextTakeover !== undefined) {
        setHumanTakeoverEnabled(nextTakeover);
      }

      const statusForAttention = nextStatus ?? conversationStatus ?? ConversationStatus.Open;
      const takeoverForAttention = nextTakeover ?? humanTakeoverEnabled;
      setRequiresAttention(requiresHostAttention(statusForAttention, takeoverForAttention));
    }
  });

  const escalate = useCallback(
    async (reason?: string) => {
      if (!conversationId || !options.guestId) {
        return;
      }

      setIsEscalating(true);
      setError(null);

      try {
        const status = await chatApi.escalateChatConversation(conversationId, options.guestId, reason);
        updateConversationState(status);
        setStatusMessage(status.guestSafeMessage || "A host has been notified and will respond as soon as possible.");
      } catch (failure) {
        handleError(failure);
      } finally {
        setIsEscalating(false);
      }
    },
    [chatApi, conversationId, handleError, options.guestId, updateConversationState]
  );

  const endConversation = useCallback(async () => {
    if (!conversationId || !options.guestId) {
      return;
    }

    setIsEnding(true);
    setError(null);

    try {
      const status = await chatApi.endChatConversation(conversationId, options.guestId);
      updateConversationState(status);
    } catch (failure) {
      handleError(failure);
    } finally {
      setIsEnding(false);
    }
  }, [chatApi, conversationId, handleError, options.guestId, updateConversationState]);

  const submitMessageFeedback = useCallback(async (messageId: string, feedbackValue: ConversationMessageFeedbackValue) => {
    if (!conversationId || !options.guestId || !messageId) {
      return;
    }

    setFeedbackSubmittingMessageId(messageId);
    try {
      const response = await chatApi.submitMessageFeedback(conversationId, messageId, {
        guestId: options.guestId,
        feedbackValue
      });

      setMessages((current) =>
        current.map((message) =>
          message.id === messageId
            ? {
                ...message,
                feedback: {
                  feedbackValue: response.feedbackValue,
                  submittedAt: response.submittedAt
                }
              }
            : message
        )
      );
    } catch (failure) {
      handleError(failure);
    } finally {
      setFeedbackSubmittingMessageId(null);
    }
  }, [chatApi, conversationId, handleError, options.guestId]);

  const startNewConversation = useCallback(() => {
    sessionStorage.removeItem(conversationStorageKey);
    setConversationId(null);
    setConversationStatus(null);
    setHumanTakeoverEnabled(false);
    setRequiresAttention(false);
    setStatusMessage(null);
    setPendingAction(null);
    setMessages([]);
    setError(null);
  }, [conversationStorageKey]);

  const confirmPendingAction = useCallback(async () => {
    const currentAction = pendingAction;
    if (!conversationId || !options.guestId || !currentAction) {
      return;
    }

    if (pendingActionExecutionRef.current === currentAction.actionId) {
      return;
    }

    pendingActionExecutionRef.current = currentAction.actionId;
    setPendingAction(null);
    setError(null);
    try {
      const response = await chatApi.confirmPendingAction(conversationId, currentAction.actionId, options.guestId);
      setConversationStatus(response.conversationStatus);
      setHumanTakeoverEnabled(response.humanTakeoverEnabled);
      setRequiresAttention(
        response.requiresHostAttention ||
          requiresHostAttention(response.conversationStatus, response.humanTakeoverEnabled)
      );
      setPendingAction(response.pendingAction ?? null);
      const returnedMessages = [response.guestMessage, response.assistantMessage].filter(Boolean) as ChatMessage[];
      setMessages((current) => mergeMessages(current, returnedMessages));
      markReadIfVisible();
    } catch (failure) {
      handleError(failure);
    } finally {
      if (pendingActionExecutionRef.current === currentAction.actionId) {
        pendingActionExecutionRef.current = null;
      }
    }
  }, [chatApi, conversationId, handleError, markReadIfVisible, options.guestId, pendingAction]);

  const cancelPendingAction = useCallback(async () => {
    const currentAction = pendingAction;
    if (!conversationId || !options.guestId || !currentAction) {
      return;
    }

    if (pendingActionExecutionRef.current === currentAction.actionId) {
      return;
    }

    pendingActionExecutionRef.current = currentAction.actionId;
    setPendingAction(null);
    setError(null);
    try {
      const response = await chatApi.cancelPendingAction(conversationId, currentAction.actionId, options.guestId);
      setConversationStatus(response.conversationStatus);
      setHumanTakeoverEnabled(response.humanTakeoverEnabled);
      setRequiresAttention(
        response.requiresHostAttention ||
          requiresHostAttention(response.conversationStatus, response.humanTakeoverEnabled)
      );
      setPendingAction(response.pendingAction ?? null);
      const returnedMessages = [response.guestMessage, response.assistantMessage].filter(Boolean) as ChatMessage[];
      setMessages((current) => mergeMessages(current, returnedMessages));
      markReadIfVisible();
    } catch (failure) {
      handleError(failure);
    } finally {
      if (pendingActionExecutionRef.current === currentAction.actionId) {
        pendingActionExecutionRef.current = null;
      }
    }
  }, [chatApi, conversationId, handleError, markReadIfVisible, options.guestId, pendingAction]);

  const startTyping = useCallback(async () => {
    await realtime.startTyping("guest");
  }, [realtime]);

  const stopTyping = useCallback(async () => {
    await realtime.stopTyping("guest");
  }, [realtime]);

  return {
    isOpen,
    isAuthenticated: Boolean(accessToken),
    isLoadingHistory,
    isSending,
    isEscalating,
    isEnding,
    feedbackSubmittingMessageId,
    error,
    unreadCount,
    conversationId,
    conversationStatus,
    humanTakeoverEnabled,
    requiresHostAttention: requiresAttention,
    statusMessage,
    pendingAction,
    messages,
    login,
    logout,
    open,
    close,
    toggle,
    sendMessage,
    loadHistory,
    escalate,
    endConversation,
    submitMessageFeedback,
    confirmPendingAction,
    cancelPendingAction,
    startNewConversation,
    clearError: () => setError(null),
    isHostTyping,
    realtimeState: realtime.connectionState,
    startTyping,
    stopTyping
  };
}
