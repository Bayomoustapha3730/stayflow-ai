import { useEffect, useMemo, useRef, useState } from "react";
import { HubConnectionState } from "@microsoft/signalr";
import { createConversationConnection } from "../realtime/conversationConnection";

interface RealtimeMessageEvent {
  conversationId: string;
  message: {
    id: string;
    conversationId: string;
    senderType: number;
    messageType: number;
    content: string;
    isInternal: boolean;
    sentAt: string;
  };
}

interface TypingEvent {
  conversationId: string;
  context: "guest" | "host" | "internal-note";
  actorUserId?: string;
  actorName?: string;
}

interface UseConversationRealtimeOptions {
  accessToken: string | null;
  conversationId: string | null;
  enabled: boolean;
  onMessageCreated?: (event: RealtimeMessageEvent) => void;
  onTypingStarted?: (event: TypingEvent) => void;
  onTypingStopped?: (event: TypingEvent) => void;
  onUnreadChanged?: () => void;
  onAssigned?: () => void;
  onReadStateChanged?: () => void;
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
  onReadStateChanged
}: UseConversationRealtimeOptions): UseConversationRealtimeResult {
  const isTestMode = import.meta.env.MODE === "test";

  const [connectionState, setConnectionState] = useState<UseConversationRealtimeResult["connectionState"]>("offline");
  const connectionRef = useRef<ReturnType<typeof createConversationConnection> | null>(null);
  const joinedConversationRef = useRef<string | null>(null);

  const baseUrl = useMemo(() => import.meta.env.VITE_STAYFLOW_API_URL ?? "http://localhost:5243", []);

  useEffect(() => {
    if (isTestMode || !enabled || !accessToken) {
      setConnectionState("offline");
      return;
    }

    const connection = createConversationConnection(baseUrl, accessToken);
    connectionRef.current = connection;
    setConnectionState("connecting");

    connection.onreconnecting(() => {
      setConnectionState("reconnecting");
    });

    connection.onreconnected(async () => {
      setConnectionState("online");

      if (conversationId) {
        try {
          await connection.invoke("JoinConversation", conversationId);
          joinedConversationRef.current = conversationId;
        } catch {
          // Ignore; polling remains the fallback path.
        }
      }

      onUnreadChanged?.();
    });

    connection.onclose(() => {
      setConnectionState("offline");
    });

    connection.on("ConversationMessageCreated", (event: RealtimeMessageEvent) => {
      onMessageCreated?.(event);
    });

    connection.on("TypingStarted", (event: TypingEvent) => {
      onTypingStarted?.(event);
    });

    connection.on("TypingStopped", (event: TypingEvent) => {
      onTypingStopped?.(event);
    });

    connection.on("ConversationUnreadCountChanged", () => {
      onUnreadChanged?.();
    });

    connection.on("ConversationAssigned", () => {
      onAssigned?.();
    });

    connection.on("ConversationReadStateChanged", () => {
      onReadStateChanged?.();
    });

    void connection
      .start()
      .then(async () => {
        setConnectionState("online");

        if (conversationId) {
          await connection.invoke("JoinConversation", conversationId);
          joinedConversationRef.current = conversationId;
        }
      })
      .catch(() => {
        setConnectionState("offline");
      });

    return () => {
      const activeConnection = connectionRef.current;
      connectionRef.current = null;
      joinedConversationRef.current = null;
      if (activeConnection) {
        void activeConnection.stop();
      }
    };
  }, [accessToken, baseUrl, conversationId, enabled, isTestMode, onAssigned, onMessageCreated, onReadStateChanged, onTypingStarted, onTypingStopped, onUnreadChanged]);

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
