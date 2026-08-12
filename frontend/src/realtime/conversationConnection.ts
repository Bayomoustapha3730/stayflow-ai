import { HubConnection, HubConnectionBuilder, HubConnectionState, HttpTransportType, LogLevel } from "@microsoft/signalr";

export type SignalRTransportSetting = "auto" | "websockets" | "serverSentEvents" | "longPolling";

export function resolveSignalRTransport(value: string | undefined | null): HttpTransportType | undefined {
  const normalized = value?.trim().toLowerCase();

  if (!normalized || normalized === "auto") {
    return undefined;
  }

  if (normalized === "websockets") {
    return HttpTransportType.WebSockets;
  }

  if (normalized === "serversentevents") {
    return HttpTransportType.ServerSentEvents;
  }

  if (normalized === "longpolling") {
    return HttpTransportType.LongPolling;
  }

  return undefined;
}

function normalizeBaseUrl(baseUrl: string): string {
  return baseUrl.replace(/\/$/, "");
}

function configuredTransport(): HttpTransportType | undefined {
  return resolveSignalRTransport(import.meta.env.VITE_SIGNALR_TRANSPORT);
}

function createConversationConnection(baseUrl: string, getAccessToken: () => string | null): HubConnection {
  const transport = configuredTransport();
  const options: {
    accessTokenFactory: () => string;
    transport?: HttpTransportType;
  } = {
    accessTokenFactory: () => getAccessToken() ?? ""
  };

  if (transport !== undefined) {
    options.transport = transport;
  }

  return new HubConnectionBuilder()
    .withUrl(`${normalizeBaseUrl(baseUrl)}/hubs/conversations`, options)
    .withAutomaticReconnect([0, 1000, 3000, 5000])
    .configureLogging(LogLevel.Warning)
    .build();
}

export function isExpectedConnectionLifecycleCancellation(error: unknown): boolean {
  const message = error instanceof Error
    ? error.message
    : typeof error === "string"
      ? error
      : "";

  const normalized = message.toLowerCase();
  return normalized.includes("connection was stopped during negotiation")
    || normalized.includes("invocation canceled")
    || normalized.includes("operation canceled")
    || normalized.includes("abort");
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
    provider?: number;
    deliveryStatus?: number | null;
    deliveredAt?: string | null;
    readAt?: string | null;
    failedAt?: string | null;
    failureCode?: string | null;
    failureReason?: string | null;
    sentAt: string;
  };
}

export interface ConversationMessageUpdatedEvent {
  conversationId: string;
  message: RealtimeMessageEvent["message"];
  timestamp?: string;
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

export interface HostCopilotWorkspaceUpdatedEvent {
  conversationId?: string;
  actionId?: string;
  eventType?: string;
  timestamp?: string;
}

export type RealtimeEventMap = {
  ConversationMessageCreated: RealtimeMessageEvent;
  ConversationMessageUpdated: ConversationMessageUpdatedEvent;
  TypingStarted: TypingEvent;
  TypingStopped: TypingEvent;
  ConversationUnreadCountChanged: ConversationUnreadCountChangedEvent;
  ConversationAssigned: ConversationAssignedEvent;
  ConversationReadStateChanged: ConversationReadStateChangedEvent;
  ConversationStateChanged: ConversationStateChangedEvent;
  HostCopilotWorkspaceUpdated: HostCopilotWorkspaceUpdatedEvent;
};

export type RealtimeConnectionState = "offline" | "connecting" | "online" | "reconnecting";

type RealtimeListener<K extends keyof RealtimeEventMap> = (payload: RealtimeEventMap[K]) => void;

interface SharedConnection {
  key: string;
  connection: HubConnection;
  accessToken: string | null;
  tenantKey: string;
  state: RealtimeConnectionState;
  stateListeners: Set<(state: RealtimeConnectionState) => void>;
  subscribers: number;
  startPromise: Promise<void> | null;
  stopPromise: Promise<void> | null;
  stopRequested: boolean;
}

const sharedConnections = new Map<string, SharedConnection>();
const sharedByConnection = new WeakMap<HubConnection, SharedConnection>();
const eventListenerRefCounts = new WeakMap<HubConnection, Map<string, Map<(payload: object) => void, number>>>();

function connectionKey(baseUrl: string): string {
  return normalizeBaseUrl(baseUrl);
}

// Hub group membership is bound to the token presented at negotiation, so the active
// organization must be part of the connection identity.
function tenantKeyFromAccessToken(accessToken: string): string {
  const payload = accessToken.split(".")[1];
  if (!payload) {
    return "";
  }

  try {
    const claims = JSON.parse(atob(payload.replace(/-/g, "+").replace(/_/g, "/"))) as { company_id?: unknown };
    return typeof claims.company_id === "string" ? claims.company_id : "";
  } catch {
    return "";
  }
}

function discardSharedConnection(shared: SharedConnection): void {
  shared.subscribers = 0;
  shared.stopRequested = true;

  if (sharedConnections.get(shared.key) === shared) {
    sharedConnections.delete(shared.key);
  }

  void stopSharedConnection(shared, true).catch(() => {
    // The replaced connection is already detached; shutdown failures cannot be surfaced.
  });
}

async function startSharedConnection(shared: SharedConnection): Promise<void> {
  if (!shared.accessToken) {
    setSharedState(shared, "offline");
    return;
  }

  if (
    shared.connection.state === HubConnectionState.Connected
    || shared.connection.state === HubConnectionState.Reconnecting
  ) {
    setSharedState(shared, shared.connection.state === HubConnectionState.Connected ? "online" : "reconnecting");
    return;
  }

  if (shared.startPromise) {
    await shared.startPromise;
    return;
  }

  if (shared.connection.state === HubConnectionState.Connecting) {
    return;
  }

  setSharedState(shared, "connecting");

  let startAttempt: Promise<void> | null = null;

  startAttempt = (async () => {
    try {
      await shared.connection.start();

      if (shared.stopRequested || shared.subscribers === 0) {
        await stopSharedConnection(shared, true);
        return;
      }

      setSharedState(shared, "online");
    } catch (error) {
      setSharedState(shared, "offline");

      if (shared.stopRequested && isExpectedConnectionLifecycleCancellation(error)) {
        return;
      }

      throw error;
    } finally {
      if (shared.startPromise === startAttempt) {
        shared.startPromise = null;
      }
    }
  })();

  shared.startPromise = startAttempt;
  await startAttempt;
}

async function stopSharedConnection(shared: SharedConnection, suppressExpectedErrors: boolean): Promise<void> {
  if (shared.stopPromise) {
    await shared.stopPromise;
    return;
  }

  const shouldStop =
    shared.connection.state === HubConnectionState.Connected
    || shared.connection.state === HubConnectionState.Connecting
    || shared.connection.state === HubConnectionState.Reconnecting;

  if (!shouldStop) {
    setSharedState(shared, "offline");
    return;
  }

  let stopAttempt: Promise<void> | null = null;

  stopAttempt = shared.connection
    .stop()
    .catch((error) => {
      if (suppressExpectedErrors && isExpectedConnectionLifecycleCancellation(error)) {
        return;
      }

      throw error;
    })
    .finally(() => {
      if (shared.stopPromise === stopAttempt) {
        shared.stopPromise = null;
      }
      setSharedState(shared, "offline");
    });

  shared.stopPromise = stopAttempt;
  await stopAttempt;
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
  const key = connectionKey(baseUrl);
  const tenantKey = tenantKeyFromAccessToken(accessToken);
  const existing = sharedConnections.get(key);
  if (existing) {
    const tenantChanged = tenantKey !== "" && existing.tenantKey !== "" && tenantKey !== existing.tenantKey;
    if (!tenantChanged) {
      existing.subscribers += 1;
      existing.stopRequested = false;
      existing.accessToken = accessToken;
      existing.tenantKey = tenantKey || existing.tenantKey;
      return existing.connection;
    }

    discardSharedConnection(existing);
  }

  let shared: SharedConnection;
  const getAccessToken = () => shared.accessToken;

  const created: SharedConnection = {
    key,
    connection: createConversationConnection(baseUrl, getAccessToken),
    accessToken,
    tenantKey,
    state: "offline",
    stateListeners: new Set(),
    subscribers: 1,
    startPromise: null,
    stopPromise: null,
    stopRequested: false
  };
  shared = created;

  wireLifecycleCallbacks(created);
  sharedConnections.set(key, created);
  sharedByConnection.set(created.connection, created);
  return created.connection;
}

export async function ensureConversationConnectionStarted(baseUrl: string): Promise<void> {
  const key = connectionKey(baseUrl);
  const shared = sharedConnections.get(key);
  if (!shared) {
    return;
  }

  shared.stopRequested = false;
  await startSharedConnection(shared);
}

export async function releaseConversationConnection(baseUrl: string, connection?: HubConnection): Promise<void> {
  const key = connectionKey(baseUrl);
  const shared = connection ? sharedByConnection.get(connection) : sharedConnections.get(key);
  if (!shared) {
    return;
  }

  shared.subscribers = Math.max(0, shared.subscribers - 1);
  if (shared.subscribers > 0) {
    return;
  }

  shared.stopRequested = true;

  try {
    await shared.startPromise;
  } catch (error) {
    if (!isExpectedConnectionLifecycleCancellation(error)) {
      throw error;
    }
  }

  if (shared.subscribers > 0) {
    return;
  }

  await stopSharedConnection(shared, true);

  if (shared.subscribers === 0 && sharedConnections.get(shared.key) === shared) {
    sharedConnections.delete(shared.key);
  }
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
  let byEvent = eventListenerRefCounts.get(connection);
  if (!byEvent) {
    byEvent = new Map<string, Map<(payload: object) => void, number>>();
    eventListenerRefCounts.set(connection, byEvent);
  }

  let eventCounts = byEvent.get(eventName);
  if (!eventCounts) {
    eventCounts = new Map<(payload: object) => void, number>();
    byEvent.set(eventName, eventCounts);
  }

  const previousCount = eventCounts.get(typedListener) ?? 0;
  if (previousCount === 0) {
    connection.on(eventName, typedListener);
  }

  eventCounts.set(typedListener, previousCount + 1);

  return () => {
    const activeByEvent = eventListenerRefCounts.get(connection);
    if (!activeByEvent) {
      connection.off(eventName, typedListener);
      return;
    }

    const activeEventCounts = activeByEvent.get(eventName);
    if (!activeEventCounts) {
      connection.off(eventName, typedListener);
      return;
    }

    const currentCount = activeEventCounts.get(typedListener) ?? 0;
    if (currentCount <= 1) {
      activeEventCounts.delete(typedListener);
      if (activeEventCounts.size == 0) {
        activeByEvent.delete(eventName);
      }
      connection.off(eventName, typedListener);
    } else {
      activeEventCounts.set(typedListener, currentCount - 1);
    }
  };
}
