import { HubConnection, HubConnectionBuilder, HttpTransportType, LogLevel } from "@microsoft/signalr";

export function createConversationConnection(baseUrl: string, accessToken: string): HubConnection {
  return new HubConnectionBuilder()
    .withUrl(`${baseUrl.replace(/\/$/, "")}/hubs/conversations`, {
      accessTokenFactory: () => accessToken,
      transport: HttpTransportType.WebSockets | HttpTransportType.LongPolling
    })
    .withAutomaticReconnect([0, 1000, 3000, 5000])
    .configureLogging(LogLevel.Warning)
    .build();
}

export interface RealtimeMessageEvent {
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

export interface TypingEvent {
  conversationId: string;
  context: "guest" | "host" | "internal-note";
  actorUserId?: string;
  actorName?: string;
}

export interface ConversationAssignedEvent {
  conversationId: string;
  assignedUser?: {
    id: string;
    fullName: string;
  } | null;
  humanTakeoverEnabled?: boolean;
  status?: string;
  timestamp?: string;
}

export interface ConversationUnreadCountChangedEvent {
  conversationId?: string;
  senderType?: string;
  participantKind?: string;
  participantId?: string;
  timestamp?: string;
}

export interface ConversationReadStateChangedEvent {
  conversationId: string;
  participantKind: string;
  participantId: string;
  lastReadAt?: string;
  lastReadMessageId?: string;
  timestamp?: string;
}

export interface ConversationStateChangedEvent {
  conversationId: string;
  status?: string;
  humanTakeoverEnabled?: boolean;
  assignedUser?: {
    id: string;
    fullName: string;
  } | null;
  assignedUserId?: string | null;
  timestamp?: string;
}

export type RealtimeEventMap = {
  ConversationMessageCreated: RealtimeMessageEvent;
  TypingStarted: TypingEvent;
  TypingStopped: TypingEvent;
  ConversationUnreadCountChanged: ConversationUnreadCountChangedEvent;
  ConversationAssigned: ConversationAssignedEvent;
  ConversationReadStateChanged: ConversationReadStateChangedEvent;
  ConversationStateChanged: ConversationStateChangedEvent;
};

export type RealtimeConnectionState = "offline" | "connecting" | "online" | "reconnecting";

type RealtimeListener<K extends keyof RealtimeEventMap> = (payload: RealtimeEventMap[K]) => void;

interface SharedConnection {
  key: string;
  connection: HubConnection;
  state: RealtimeConnectionState;
  stateListeners: Set<(state: RealtimeConnectionState) => void>;
  subscribers: number;
  startPromise: Promise<void> | null;
}

const sharedConnections = new Map<string, SharedConnection>();
const sharedByConnection = new WeakMap<HubConnection, SharedConnection>();

function connectionKey(baseUrl: string, accessToken: string): string {
  return `${baseUrl.replace(/\/$/, "")}|${accessToken}`;
}

async function startSharedConnection(shared: SharedConnection): Promise<void> {
  if (shared.connection.state !== "Disconnected") {
    return;
  }

  setSharedState(shared, "connecting");

  if (!shared.startPromise) {
    shared.startPromise = shared.connection.start().finally(() => {
      shared.startPromise = null;
    });
  }

  await shared.startPromise;
  setSharedState(shared, "online");
}

function setSharedState(shared: SharedConnection, state: RealtimeConnectionState): void {
  if (shared.state === state) {
    return;
  }

  shared.state = state;
  for (const listener of shared.stateListeners) {
    listener(state);
  }
}

function wireLifecycleCallbacks(shared: SharedConnection): void {
  shared.connection.onreconnecting(() => {
    setSharedState(shared, "reconnecting");
  });

  shared.connection.onreconnected(() => {
    setSharedState(shared, "online");
  });

  shared.connection.onclose(() => {
    setSharedState(shared, "offline");
  });
}

export function acquireConversationConnection(baseUrl: string, accessToken: string): HubConnection {
  const key = connectionKey(baseUrl, accessToken);
  const existing = sharedConnections.get(key);
  if (existing) {
    existing.subscribers += 1;
    return existing.connection;
  }

  const created: SharedConnection = {
    key,
    connection: createConversationConnection(baseUrl, accessToken),
    state: "offline",
    stateListeners: new Set(),
    subscribers: 1,
    startPromise: null
  };
  wireLifecycleCallbacks(created);
  sharedConnections.set(key, created);
  sharedByConnection.set(created.connection, created);
  return created.connection;
}

export async function ensureConversationConnectionStarted(baseUrl: string, accessToken: string): Promise<void> {
  const key = connectionKey(baseUrl, accessToken);
  const shared = sharedConnections.get(key);
  if (!shared) {
    return;
  }

  await startSharedConnection(shared);
}

export async function releaseConversationConnection(baseUrl: string, accessToken: string): Promise<void> {
  const key = connectionKey(baseUrl, accessToken);
  const shared = sharedConnections.get(key);
  if (!shared) {
    return;
  }

  shared.subscribers -= 1;
  if (shared.subscribers > 0) {
    return;
  }

  sharedConnections.delete(key);
  if (shared.connection.state !== "Disconnected") {
    await shared.connection.stop();
  }
  setSharedState(shared, "offline");
}

export function subscribeConversationConnectionState(
  connection: HubConnection,
  listener: (state: RealtimeConnectionState) => void
): () => void {
  const shared = sharedByConnection.get(connection);
  if (!shared) {
    listener("offline");
    return () => {};
  }

  shared.stateListeners.add(listener);
  listener(shared.state);

  return () => {
    shared.stateListeners.delete(listener);
  };
}

export function onConversationRealtimeEvent<K extends keyof RealtimeEventMap>(
  connection: HubConnection,
  eventName: K,
  listener: RealtimeListener<K>
): () => void {
  const typedListener = listener as (payload: object) => void;
  connection.on(eventName, typedListener);
  return () => {
    connection.off(eventName, typedListener);
  };
}
