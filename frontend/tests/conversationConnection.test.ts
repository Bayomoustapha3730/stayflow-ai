import { beforeEach, describe, expect, it, vi } from "vitest";

type Deferred<T> = {
  promise: Promise<T>;
  resolve: (value: T | PromiseLike<T>) => void;
  reject: (reason?: unknown) => void;
};

function createDeferred<T>(): Deferred<T> {
  let resolve!: (value: T | PromiseLike<T>) => void;
  let reject!: (reason?: unknown) => void;

  const promise = new Promise<T>((resolvePromise, rejectPromise) => {
    resolve = resolvePromise;
    reject = rejectPromise;
  });

  return { promise, resolve, reject };
}

vi.mock("@microsoft/signalr", () => {
  const createdConnections: MockHubConnection[] = [];

  const HubConnectionState = {
    Disconnected: "Disconnected",
    Connecting: "Connecting",
    Connected: "Connected",
    Reconnecting: "Reconnecting"
  } as const;

  const HttpTransportType = {
    WebSockets: 1,
    ServerSentEvents: 2,
    LongPolling: 4
  } as const;

  class MockHubConnection {
    public state: (typeof HubConnectionState)[keyof typeof HubConnectionState] = HubConnectionState.Disconnected;
    public readonly invokeCalls: Array<{ methodName: string; args: unknown[] }> = [];
    public startCalls = 0;
    public stopCalls = 0;
    public startImpl: (() => Promise<void>) | null = null;
    public stopImpl: (() => Promise<void>) | null = null;
    public readonly options: { accessTokenFactory: () => string; transport?: number };

    private readonly eventHandlers = new Map<string, Set<(payload: object) => void>>();
    private reconnectingHandler: (() => void) | null = null;
    private reconnectedHandler: (() => void) | null = null;
    private closeHandler: (() => void) | null = null;

    public constructor(options: { accessTokenFactory: () => string; transport?: number }) {
      this.options = options;
    }

    public start(): Promise<void> {
      this.startCalls += 1;
      this.state = HubConnectionState.Connecting;

      const attempt = this.startImpl ? this.startImpl() : Promise.resolve();
      return attempt.then(() => {
        this.state = HubConnectionState.Connected;
      });
    }

    public stop(): Promise<void> {
      this.stopCalls += 1;
      const attempt = this.stopImpl ? this.stopImpl() : Promise.resolve();
      return attempt.then(() => {
        this.state = HubConnectionState.Disconnected;
        this.closeHandler?.();
      });
    }

    public invoke(methodName: string, ...args: unknown[]): Promise<void> {
      this.invokeCalls.push({ methodName, args });
      return Promise.resolve();
    }

    public on(eventName: string, handler: (payload: object) => void): void {
      const handlers = this.eventHandlers.get(eventName) ?? new Set<(payload: object) => void>();
      handlers.add(handler);
      this.eventHandlers.set(eventName, handlers);
    }

    public off(eventName: string, handler: (payload: object) => void): void {
      this.eventHandlers.get(eventName)?.delete(handler);
    }

    public onreconnecting(handler: () => void): void {
      this.reconnectingHandler = handler;
    }

    public onreconnected(handler: () => void): void {
      this.reconnectedHandler = handler;
    }

    public onclose(handler: () => void): void {
      this.closeHandler = handler;
    }

    public triggerReconnecting(): void {
      this.state = HubConnectionState.Reconnecting;
      this.reconnectingHandler?.();
    }

    public triggerReconnected(): void {
      this.state = HubConnectionState.Connected;
      this.reconnectedHandler?.();
    }

    public getEventHandlerCount(eventName: string): number {
      return this.eventHandlers.get(eventName)?.size ?? 0;
    }
  }

  class HubConnectionBuilder {
    private options: { accessTokenFactory: () => string; transport?: number } = {
      accessTokenFactory: () => ""
    };

    public withUrl(_url: string, options: { accessTokenFactory: () => string; transport?: number }): HubConnectionBuilder {
      this.options = options;
      return this;
    }

    public withAutomaticReconnect(_delays: number[]): HubConnectionBuilder {
      return this;
    }

    public configureLogging(_level: number): HubConnectionBuilder {
      return this;
    }

    public build(): MockHubConnection {
      const connection = new MockHubConnection(this.options);
      createdConnections.push(connection);
      return connection;
    }
  }

  return {
    HubConnectionBuilder,
    HubConnectionState,
    HttpTransportType,
    LogLevel: { Warning: 3 },
    __testing: {
      createdConnections,
      reset() {
        createdConnections.length = 0;
      }
    }
  };
});

async function loadModule(signalRTransport?: string) {
  vi.resetModules();

  if (signalRTransport === undefined) {
    vi.unstubAllEnvs();
  } else {
    vi.stubEnv("VITE_SIGNALR_TRANSPORT", signalRTransport);
  }

  const mod = await import("../src/realtime/conversationConnection");
  const signalR = await import("@microsoft/signalr") as unknown as {
    HttpTransportType: { WebSockets: number; ServerSentEvents: number; LongPolling: number };
    __testing: {
      createdConnections: Array<{ startCalls: number; stopCalls: number; options: { accessTokenFactory: () => string; transport?: number }; startImpl: (() => Promise<void>) | null }>;
      reset: () => void;
    };
  };

  signalR.__testing.reset();
  return { mod, signalR };
}

beforeEach(() => {
  vi.restoreAllMocks();
  vi.unstubAllEnvs();
});

function tokenForCompany(companyId: string, nonce = "n"): string {
  const payload = btoa(JSON.stringify({ company_id: companyId, nonce })).replace(/=+$/, "");
  return `header.${payload}.signature`;
}

describe("conversationConnection", () => {
  it("maps transport values and falls back to auto for invalid values", async () => {
    const { mod, signalR } = await loadModule();

    expect(mod.resolveSignalRTransport("auto")).toBeUndefined();
    expect(mod.resolveSignalRTransport("websockets")).toBe(signalR.HttpTransportType.WebSockets);
    expect(mod.resolveSignalRTransport("serverSentEvents")).toBe(signalR.HttpTransportType.ServerSentEvents);
    expect(mod.resolveSignalRTransport("longPolling")).toBe(signalR.HttpTransportType.LongPolling);
    expect(mod.resolveSignalRTransport("not-a-transport")).toBeUndefined();
  });

  it("uses auto transport by default", async () => {
    const { mod, signalR } = await loadModule();
    mod.acquireConversationConnection("http://localhost:5243", "token");

    expect(signalR.__testing.createdConnections).toHaveLength(1);
    expect(signalR.__testing.createdConnections[0].options.transport).toBeUndefined();
  });

  it("forces long polling when configured", async () => {
    const { mod, signalR } = await loadModule("longPolling");
    mod.acquireConversationConnection("http://localhost:5243", "token");

    expect(signalR.__testing.createdConnections).toHaveLength(1);
    expect(signalR.__testing.createdConnections[0].options.transport).toBe(signalR.HttpTransportType.LongPolling);
  });

  it("reuses one start attempt across concurrent subscribers", async () => {
    const { mod, signalR } = await loadModule();

    mod.acquireConversationConnection("http://localhost:5243", "token");
    mod.acquireConversationConnection("http://localhost:5243", "token");

    const connection = signalR.__testing.createdConnections[0];
    const deferred = createDeferred<void>();
    connection.startImpl = () => deferred.promise;

    const first = mod.ensureConversationConnectionStarted("http://localhost:5243");
    const second = mod.ensureConversationConnectionStarted("http://localhost:5243");

    expect(connection.startCalls).toBe(1);

    deferred.resolve();
    await Promise.all([first, second]);

    await mod.releaseConversationConnection("http://localhost:5243");
    await mod.releaseConversationConnection("http://localhost:5243");

    expect(connection.stopCalls).toBe(1);
  });

  it("waits for startup before stop during cleanup", async () => {
    const { mod, signalR } = await loadModule();

    mod.acquireConversationConnection("http://localhost:5243", "token");
    const connection = signalR.__testing.createdConnections[0];

    const deferred = createDeferred<void>();
    connection.startImpl = () => deferred.promise;

    const startPromise = mod.ensureConversationConnectionStarted("http://localhost:5243");
    const releasePromise = mod.releaseConversationConnection("http://localhost:5243");

    expect(connection.stopCalls).toBe(0);

    deferred.resolve();

    await startPromise;
    await releasePromise;

    expect(connection.stopCalls).toBe(1);
  });

  it("stops idempotently when released more than once", async () => {
    const { mod, signalR } = await loadModule();

    mod.acquireConversationConnection("http://localhost:5243", "token");
    const connection = signalR.__testing.createdConnections[0];

    await mod.ensureConversationConnectionStarted("http://localhost:5243");
    await mod.releaseConversationConnection("http://localhost:5243");
    await mod.releaseConversationConnection("http://localhost:5243");

    expect(connection.stopCalls).toBe(1);
  });

  it("skips start when token is missing", async () => {
    const { mod, signalR } = await loadModule();

    mod.acquireConversationConnection("http://localhost:5243", "");
    const connection = signalR.__testing.createdConnections[0];

    await mod.ensureConversationConnectionStarted("http://localhost:5243");

    expect(connection.startCalls).toBe(0);
  });

  it("accessTokenFactory reads the latest token value", async () => {
    const { mod, signalR } = await loadModule();

    mod.acquireConversationConnection("http://localhost:5243", "old-token");
    mod.acquireConversationConnection("http://localhost:5243", "new-token");

    const connection = signalR.__testing.createdConnections[0];
    expect(connection.options.accessTokenFactory()).toBe("new-token");

    await mod.releaseConversationConnection("http://localhost:5243");
    await mod.releaseConversationConnection("http://localhost:5243");
  });

  it("replaces the shared connection when the organization changes", async () => {
    const { mod, signalR } = await loadModule();

    mod.acquireConversationConnection("http://localhost:5243", tokenForCompany("company-a"));
    const organizationAConnection = signalR.__testing.createdConnections[0];
    await mod.ensureConversationConnectionStarted("http://localhost:5243");

    mod.acquireConversationConnection("http://localhost:5243", tokenForCompany("company-b"));

    expect(signalR.__testing.createdConnections).toHaveLength(2);
    expect(organizationAConnection.stopCalls).toBe(1);

    const organizationBConnection = signalR.__testing.createdConnections[1];
    expect(organizationBConnection.options.accessTokenFactory()).toBe(tokenForCompany("company-b"));
  });

  it("reuses the shared connection when the token is refreshed for the same organization", async () => {
    const { mod, signalR } = await loadModule();

    mod.acquireConversationConnection("http://localhost:5243", tokenForCompany("company-a", "first"));
    mod.acquireConversationConnection("http://localhost:5243", tokenForCompany("company-a", "second"));

    expect(signalR.__testing.createdConnections).toHaveLength(1);
    expect(signalR.__testing.createdConnections[0].options.accessTokenFactory())
      .toBe(tokenForCompany("company-a", "second"));
  });

  it("treats cleanup cancellation during negotiation as expected", async () => {    const { mod, signalR } = await loadModule();

    mod.acquireConversationConnection("http://localhost:5243", "token");
    const connection = signalR.__testing.createdConnections[0];

    connection.startImpl = () => Promise.reject(new Error("The connection was stopped during negotiation."));

    const startPromise = mod.ensureConversationConnectionStarted("http://localhost:5243");
    const releasePromise = mod.releaseConversationConnection("http://localhost:5243");

    await expect(startPromise).resolves.toBeUndefined();
    await expect(releasePromise).resolves.toBeUndefined();
  });

  it("surfaces real startup failures", async () => {
    const { mod, signalR } = await loadModule();

    mod.acquireConversationConnection("http://localhost:5243", "token");
    const connection = signalR.__testing.createdConnections[0];

    connection.startImpl = () => Promise.reject(new Error("startup exploded"));

    await expect(mod.ensureConversationConnectionStarted("http://localhost:5243")).rejects.toThrow("startup exploded");
  });

  it("deduplicates event subscriptions for the same listener", async () => {
    const { mod, signalR } = await loadModule();

    const connection = mod.acquireConversationConnection("http://localhost:5243", "token");
    const testConnection = connection as unknown as {
      getEventHandlerCount: (eventName: string) => number;
    };

    const listener = vi.fn();
    const unsubscribeOne = mod.onConversationRealtimeEvent(connection, "HostCopilotWorkspaceUpdated", listener);
    const unsubscribeTwo = mod.onConversationRealtimeEvent(connection, "HostCopilotWorkspaceUpdated", listener);

    expect(testConnection.getEventHandlerCount("HostCopilotWorkspaceUpdated")).toBe(1);

    unsubscribeOne();
    expect(testConnection.getEventHandlerCount("HostCopilotWorkspaceUpdated")).toBe(1);

    unsubscribeTwo();
    expect(testConnection.getEventHandlerCount("HostCopilotWorkspaceUpdated")).toBe(0);

    await mod.releaseConversationConnection("http://localhost:5243");
    expect(signalR.__testing.createdConnections.length).toBeGreaterThan(0);
  });
});
