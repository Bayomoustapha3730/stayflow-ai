import { useEffect, useMemo, useRef, useState } from "react";
import { HubConnectionState } from "@microsoft/signalr";
import {
  acquireConversationConnection,
  ensureConversationConnectionStarted,
  onConversationRealtimeEvent,
  releaseConversationConnection,
  subscribeConversationConnectionState,
  RealtimeMessageEvent,
  TypingEvent,
  ConversationUnreadCountChangedEvent,
  ConversationAssignedEvent,
  ConversationReadStateChangedEvent,
  ConversationStateChangedEvent
} from "../realtime/conversationConnection";

interface UseConversationRealtimeOptions {
  accessToken: string | null;
  conversationId: string | null;
  enabled: boolean;
  onMessageCreated?: (event: RealtimeMessageEvent) => void;
  onTypingStarted?: (event: TypingEvent) => void;
  onTypingStopped?: (event: TypingEvent) => void;
  onUnreadChanged?: (event: ConversationUnreadCountChangedEvent) => void;
  onAssigned?: (event: ConversationAssignedEvent) => void;
  onReadStateChanged?: (event: ConversationReadStateChangedEvent) => void;
  onStateChanged?: (event: ConversationStateChangedEvent) => void;
}

export interface UseConversationRealtimeResult {
  connectionState: "offline" | "connecting" | "online" | "reconnecting";
  startTyping: (context: "guest" | "host" | "internal-note") => Promise<void>;
  stopTyping: (context: "guest" | "host" | "internal-note") => Promise<void>;
}

const defaultResult: UseConversationRealtimeResult = {
  connectionState: "offline",
  startTyping: async () => {},
  stopTyping: async () => {}
};

export function useConversationRealtime({
  accessToken,
  conversationId,
  enabled,
  onMessageCreated,
  onTypingStarted,
  onTypingStopped,
  onUnreadChanged,
  onAssigned,
  onReadStateChanged,
  onStateChanged
}: UseConversationRealtimeOptions): UseConversationRealtimeResult {
  const isTestMode = import.meta.env.MODE === "test";

  const [connectionState, setConnectionState] = useState<UseConversationRealtimeResult["connectionState"]>("offline");
  const connectionRef = useRef<ReturnType<typeof acquireConversationConnection> | null>(null);
  const joinedConversationRef = useRef<string | null>(null);
  const latestConversationIdRef = useRef<string | null>(conversationId);
  const callbacksRef = useRef({
    onMessageCreated,
    onTypingStarted,
    onTypingStopped,
    onUnreadChanged,
    onAssigned,
    onReadStateChanged,
    onStateChanged
  });

  useEffect(() => {
    callbacksRef.current = {
      onMessageCreated,
      onTypingStarted,
      onTypingStopped,
      onUnreadChanged,
      onAssigned,
      onReadStateChanged,
      onStateChanged
    };
  }, [onAssigned, onMessageCreated, onReadStateChanged, onStateChanged, onTypingStarted, onTypingStopped, onUnreadChanged]);

  useEffect(() => {
    latestConversationIdRef.current = conversationId;
  }, [conversationId]);

  const baseUrl = useMemo(() => import.meta.env.VITE_STAYFLOW_API_URL ?? "http://localhost:5243", []);

  useEffect(() => {
    if (isTestMode || !enabled || !accessToken) {
      setConnectionState("offline");
      return;
    }

    const connection = acquireConversationConnection(baseUrl, accessToken);
    connectionRef.current = connection;
    setConnectionState(connection.state === HubConnectionState.Connected ? "online" : "connecting");

    const unsubscribeConnectionState = subscribeConversationConnectionState(connection, (state) => {
      setConnectionState(state);

      if (state !== "online") {
        return;
      }

      const nextConversationId = latestConversationIdRef.current;
      const previousConversationId = joinedConversationRef.current;

      if (previousConversationId && previousConversationId !== nextConversationId) {
        void connection.invoke("LeaveConversation", previousConversationId).catch(() => {});
        joinedConversationRef.current = null;
      }

      if (nextConversationId && nextConversationId !== joinedConversationRef.current) {
        void connection.invoke("JoinConversation", nextConversationId)
          .then(() => {
            joinedConversationRef.current = nextConversationId;
          })
          .catch(() => {
            // Ignore; polling remains the fallback path.
          });
      }

      callbacksRef.current.onUnreadChanged?.({
        conversationId: nextConversationId ?? undefined,
        timestamp: new Date().toISOString()
      });
    });

    const unsubscribers = [
      onConversationRealtimeEvent(connection, "ConversationMessageCreated", (event: RealtimeMessageEvent) => {
        callbacksRef.current.onMessageCreated?.(event);
      }),
      onConversationRealtimeEvent(connection, "TypingStarted", (event: TypingEvent) => {
        callbacksRef.current.onTypingStarted?.(event);
      }),
      onConversationRealtimeEvent(connection, "TypingStopped", (event: TypingEvent) => {
        callbacksRef.current.onTypingStopped?.(event);
      }),
      onConversationRealtimeEvent(connection, "ConversationUnreadCountChanged", (event: ConversationUnreadCountChangedEvent) => {
        callbacksRef.current.onUnreadChanged?.(event);
      }),
      onConversationRealtimeEvent(connection, "ConversationAssigned", (event: ConversationAssignedEvent) => {
        callbacksRef.current.onAssigned?.(event);
      }),
      onConversationRealtimeEvent(connection, "ConversationReadStateChanged", (event: ConversationReadStateChangedEvent) => {
        callbacksRef.current.onReadStateChanged?.(event);
      }),
      onConversationRealtimeEvent(connection, "ConversationStateChanged", (event: ConversationStateChangedEvent) => {
        callbacksRef.current.onStateChanged?.(event);
      })
    ];

    void ensureConversationConnectionStarted(baseUrl, accessToken)
      .then(async () => {
        setConnectionState("online");

        const nextConversationId = latestConversationIdRef.current;
        if (nextConversationId) {
          try {
            await connection.invoke("JoinConversation", nextConversationId);
            joinedConversationRef.current = nextConversationId;
          } catch {
            // Ignore; polling remains the fallback path.
          }
        }
      })
      .catch(() => {
        setConnectionState("offline");
      });

    return () => {
      const activeConnection = connectionRef.current;
      connectionRef.current = null;
      const joinedConversationId = joinedConversationRef.current;
      joinedConversationRef.current = null;

      if (activeConnection && joinedConversationId && activeConnection.state === HubConnectionState.Connected) {
        void activeConnection.invoke("LeaveConversation", joinedConversationId).catch(() => {});
      }

      for (const unsubscribe of unsubscribers) {
        unsubscribe();
      }

      unsubscribeConnectionState();

      if (activeConnection) {
        void releaseConversationConnection(baseUrl, accessToken);
      }
    };
  }, [accessToken, baseUrl, enabled, isTestMode]);

  useEffect(() => {
    const connection = connectionRef.current;
    if (isTestMode || !connection || connection.state !== HubConnectionState.Connected) {
      return;
    }

    const previousConversationId = joinedConversationRef.current;
    if (previousConversationId && previousConversationId !== conversationId) {
      void connection.invoke("LeaveConversation", previousConversationId);
      joinedConversationRef.current = null;
    }

    if (conversationId && conversationId !== joinedConversationRef.current) {
      void connection.invoke("JoinConversation", conversationId);
      joinedConversationRef.current = conversationId;
    }
  }, [conversationId, isTestMode]);

  async function startTyping(context: "guest" | "host" | "internal-note") {
    const connection = connectionRef.current;
    if (isTestMode || !connection || !conversationId || connection.state !== HubConnectionState.Connected) {
      return;
    }

    await connection.invoke("StartTyping", conversationId, context);
  }

  async function stopTyping(context: "guest" | "host" | "internal-note") {
    const connection = connectionRef.current;
    if (isTestMode || !connection || !conversationId || connection.state !== HubConnectionState.Connected) {
      return;
    }

    await connection.invoke("StopTyping", conversationId, context);
  }

  if (isTestMode || !enabled || !accessToken) {
    return defaultResult;
  }

  return {
    connectionState,
    startTyping,
    stopTyping
  };
}
