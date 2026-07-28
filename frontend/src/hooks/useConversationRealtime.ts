import { useEffect, useMemo, useRef, useState } from "react";
import { HubConnectionState } from "@microsoft/signalr";
import {
  acquireConversationConnection,
  ensureConversationConnectionStarted,
  isExpectedConnectionLifecycleCancellation,
  onConversationRealtimeEvent,
  releaseConversationConnection,
  subscribeConversationConnectionState,
  RealtimeMessageEvent,
  ConversationMessageUpdatedEvent,
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
  onMessageUpdated?: (event: ConversationMessageUpdatedEvent) => void;
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
  onMessageUpdated,
  onTypingStarted,
  onTypingStopped,
  onUnreadChanged,
  onAssigned,
  onReadStateChanged,
  onStateChanged
}: UseConversationRealtimeOptions): UseConversationRealtimeResult {
  const isTestMode = import.meta.env.MODE === "test" && import.meta.env.VITE_ENABLE_REALTIME_IN_TESTS !== "true";

  const [connectionState, setConnectionState] = useState<UseConversationRealtimeResult["connectionState"]>("offline");
  const connectionRef = useRef<ReturnType<typeof acquireConversationConnection> | null>(null);
  const joinedConversationRef = useRef<string | null>(null);
  const syncMembershipRef = useRef<(() => Promise<void>) | null>(null);
  const membershipSyncPromiseRef = useRef<Promise<void> | null>(null);
  const latestConversationIdRef = useRef<string | null>(conversationId);
  const callbacksRef = useRef({
    onMessageCreated,
    onMessageUpdated,
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
      onMessageUpdated,
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

    let disposed = false;

    const connection = acquireConversationConnection(baseUrl, accessToken);
    connectionRef.current = connection;
    setConnectionState(connection.state === HubConnectionState.Connected ? "online" : "connecting");

    const syncConversationMembership = async () => {
      if (disposed || connectionRef.current !== connection || connection.state !== HubConnectionState.Connected) {
        return;
      }

      if (membershipSyncPromiseRef.current) {
        await membershipSyncPromiseRef.current;
        return;
      }

      const syncAttempt = (async () => {
        const targetConversationId = latestConversationIdRef.current;
        const previousConversationId = joinedConversationRef.current;

        if (previousConversationId && previousConversationId !== targetConversationId) {
          try {
            await connection.invoke("LeaveConversation", previousConversationId);
          } catch (error) {
            if (!(disposed && isExpectedConnectionLifecycleCancellation(error))) {
              console.warn("StayFlow realtime leave conversation failed.", error);
            }
          } finally {
            if (joinedConversationRef.current === previousConversationId) {
              joinedConversationRef.current = null;
            }
          }
        }

        const currentConversationId = latestConversationIdRef.current;
        if (!currentConversationId || currentConversationId === joinedConversationRef.current) {
          return;
        }

        try {
          await connection.invoke("JoinConversation", currentConversationId);
          joinedConversationRef.current = currentConversationId;
        } catch (error) {
          if (!(disposed && isExpectedConnectionLifecycleCancellation(error))) {
            console.warn("StayFlow realtime join conversation failed.", error);
          }
        }
      })();

      membershipSyncPromiseRef.current = syncAttempt;

      try {
        await syncAttempt;
      } finally {
        if (membershipSyncPromiseRef.current === syncAttempt) {
          membershipSyncPromiseRef.current = null;
        }
      }
    };

    syncMembershipRef.current = syncConversationMembership;

    const unsubscribeConnectionState = subscribeConversationConnectionState(connection, (state) => {
      setConnectionState(state);

      if (state === "offline" || state === "reconnecting") {
        joinedConversationRef.current = null;
      }

      if (state !== "online") {
        return;
      }

      void syncConversationMembership();

      callbacksRef.current.onUnreadChanged?.({
        conversationId: latestConversationIdRef.current ?? undefined,
        timestamp: new Date().toISOString()
      });
    });

    const unsubscribers = [
      onConversationRealtimeEvent(connection, "ConversationMessageCreated", (event: RealtimeMessageEvent) => {
        callbacksRef.current.onMessageCreated?.(event);
      }),
      onConversationRealtimeEvent(connection, "ConversationMessageUpdated", (event: ConversationMessageUpdatedEvent) => {
        callbacksRef.current.onMessageUpdated?.(event);
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

    void ensureConversationConnectionStarted(baseUrl)
      .then(() => {
        if (!disposed) {
          setConnectionState((current) =>
            connection.state === HubConnectionState.Connected ? "online" : current
          );
        }
      })
      .catch((error) => {
        if (disposed && isExpectedConnectionLifecycleCancellation(error)) {
          return;
        }

        console.error("StayFlow realtime connection failed to start.", error);
        setConnectionState("offline");
      });

    return () => {
      disposed = true;
      syncMembershipRef.current = null;

      const activeConnection = connectionRef.current;
      connectionRef.current = null;
      const joinedConversationId = joinedConversationRef.current;
      joinedConversationRef.current = null;

      if (activeConnection && joinedConversationId && activeConnection.state === HubConnectionState.Connected) {
        void activeConnection.invoke("LeaveConversation", joinedConversationId).catch((error: unknown) => {
          if (!isExpectedConnectionLifecycleCancellation(error)) {
            console.warn("StayFlow realtime leave conversation cleanup failed.", error);
          }
        });
      }

      for (const unsubscribe of unsubscribers) {
        unsubscribe();
      }

      unsubscribeConnectionState();

      if (activeConnection) {
        void releaseConversationConnection(baseUrl).catch((error) => {
          if (!isExpectedConnectionLifecycleCancellation(error)) {
            console.error("StayFlow realtime connection cleanup failed.", error);
          }
        });
      }
    };
  }, [accessToken, baseUrl, enabled, isTestMode]);

  useEffect(() => {
    const connection = connectionRef.current;
    if (isTestMode || !connection || connection.state !== HubConnectionState.Connected) {
      return;
    }

    const previousConversationId = joinedConversationRef.current;
    if (previousConversationId === conversationId) {
      return;
    }

    void syncMembershipRef.current?.();
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
