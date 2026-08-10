import React, { StrictMode } from "react";
import { act, renderHook } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

const mocks = vi.hoisted(() => {
  type ConnectionState = "offline" | "connecting" | "online" | "reconnecting";

  const fakeConnection = {
    state: "Disconnected",
    invoke: vi.fn((..._args: unknown[]) => Promise.resolve())
  };

  const stateListeners = new Set<(state: ConnectionState) => void>();
  const eventUnsubscribers: Array<ReturnType<typeof vi.fn>> = [];

  const acquireConversationConnection = vi.fn(() => fakeConnection as never);
  const ensureConversationConnectionStarted = vi.fn(() => Promise.resolve());
  const releaseConversationConnection = vi.fn(() => Promise.resolve());
  const subscribeConversationConnectionState = vi.fn((_connection: unknown, listener: (state: ConnectionState) => void) => {
    stateListeners.add(listener);
    listener("offline");
    return () => {
      stateListeners.delete(listener);
    };
  });
  const onConversationRealtimeEvent = vi.fn(() => {
    const unsubscribe = vi.fn();
    eventUnsubscribers.push(unsubscribe);
    return unsubscribe;
  });
  const isExpectedConnectionLifecycleCancellation = vi.fn((error: unknown) => {
    const message = error instanceof Error ? error.message : String(error);
    return message.toLowerCase().includes("stopped during negotiation");
  });

  return {
    fakeConnection,
    stateListeners,
    eventUnsubscribers,
    acquireConversationConnection,
    ensureConversationConnectionStarted,
    releaseConversationConnection,
    subscribeConversationConnectionState,
    onConversationRealtimeEvent,
    isExpectedConnectionLifecycleCancellation
  };
});

vi.mock("../src/realtime/conversationConnection", () => ({
  acquireConversationConnection: mocks.acquireConversationConnection,
  ensureConversationConnectionStarted: mocks.ensureConversationConnectionStarted,
  releaseConversationConnection: mocks.releaseConversationConnection,
  subscribeConversationConnectionState: mocks.subscribeConversationConnectionState,
  onConversationRealtimeEvent: mocks.onConversationRealtimeEvent,
  isExpectedConnectionLifecycleCancellation: mocks.isExpectedConnectionLifecycleCancellation
}));

import { useConversationRealtime } from "../src/hooks/useConversationRealtime";

type ConnectionState = "offline" | "connecting" | "online" | "reconnecting";

function emitState(state: ConnectionState) {
  mocks.fakeConnection.state = state === "online"
    ? "Connected"
    : state === "connecting"
      ? "Connecting"
      : state === "reconnecting"
        ? "Reconnecting"
        : "Disconnected";

  for (const listener of mocks.stateListeners) {
    listener(state);
  }
}

async function flushAsyncWork() {
  await act(async () => {
    await Promise.resolve();
  });
}

beforeEach(() => {
  vi.clearAllMocks();
  vi.stubEnv("VITE_ENABLE_REALTIME_IN_TESTS", "true");
  mocks.eventUnsubscribers.length = 0;
  mocks.stateListeners.clear();
  mocks.fakeConnection.state = "Disconnected";
  mocks.fakeConnection.invoke.mockClear();
  mocks.ensureConversationConnectionStarted.mockResolvedValue(undefined);
  mocks.releaseConversationConnection.mockResolvedValue(undefined);
});

afterEach(() => {
  vi.unstubAllEnvs();
  vi.restoreAllMocks();
});

describe("useConversationRealtime", () => {
  it("does not start when token is missing", () => {
    renderHook(() =>
      useConversationRealtime({
        accessToken: null,
        conversationId: "conversation-1",
        enabled: true
      })
    );

    expect(mocks.acquireConversationConnection).not.toHaveBeenCalled();
    expect(mocks.ensureConversationConnectionStarted).not.toHaveBeenCalled();
  });

  it("uses connecting state before the shared connection reports online", async () => {
    const { result } = renderHook(() =>
      useConversationRealtime({
        accessToken: "token",
        conversationId: "conversation-1",
        enabled: true
      })
    );

    await flushAsyncWork();

    expect(result.current.connectionState).toBe("connecting");
  });

  it("joins once after connection becomes online", async () => {
    renderHook(() =>
      useConversationRealtime({
        accessToken: "token",
        conversationId: "conversation-1",
        enabled: true
      })
    );

    await flushAsyncWork();

    await act(async () => {
      emitState("online");
      await Promise.resolve();
    });

    expect(mocks.fakeConnection.invoke).toHaveBeenCalledTimes(1);
    expect(mocks.fakeConnection.invoke).toHaveBeenCalledWith("JoinConversation", "conversation-1");

    await act(async () => {
      emitState("online");
      await Promise.resolve();
    });

    expect(mocks.fakeConnection.invoke).toHaveBeenCalledTimes(1);
  });

  it("rejoins conversation after reconnect", async () => {
    renderHook(() =>
      useConversationRealtime({
        accessToken: "token",
        conversationId: "conversation-1",
        enabled: true
      })
    );

    await flushAsyncWork();

    await act(async () => {
      emitState("online");
      await Promise.resolve();
    });

    await act(async () => {
      emitState("reconnecting");
      emitState("online");
      await Promise.resolve();
    });

    const joinCalls = mocks.fakeConnection.invoke.mock.calls.filter((call) => call[0] === "JoinConversation");
    expect(joinCalls).toHaveLength(2);
  });

  it("handles StrictMode mount/unmount/remount without leaking handlers", async () => {
    const wrapper = ({ children }: { children: React.ReactNode }) => <StrictMode>{children}</StrictMode>;

    const { unmount } = renderHook(
      () =>
        useConversationRealtime({
          accessToken: "token",
          conversationId: "conversation-1",
          enabled: true
        }),
      { wrapper }
    );

    await flushAsyncWork();
    unmount();

    for (const unsubscribe of mocks.eventUnsubscribers) {
      expect(unsubscribe).toHaveBeenCalledTimes(1);
    }

    expect(mocks.releaseConversationConnection).toHaveBeenCalled();
  });

  it("does not log expected cleanup cancellation as an error", async () => {
    const errorSpy = vi.spyOn(console, "error").mockImplementation(() => {});

    mocks.ensureConversationConnectionStarted.mockRejectedValueOnce(
      new Error("The connection was stopped during negotiation.")
    );

    const { unmount } = renderHook(() =>
      useConversationRealtime({
        accessToken: "token",
        conversationId: "conversation-1",
        enabled: true
      })
    );

    unmount();
    await flushAsyncWork();

    expect(errorSpy).not.toHaveBeenCalled();
  });

  it("logs unexpected startup failures", async () => {
    const errorSpy = vi.spyOn(console, "error").mockImplementation(() => {});

    mocks.ensureConversationConnectionStarted.mockRejectedValueOnce(new Error("startup failure"));

    renderHook(() =>
      useConversationRealtime({
        accessToken: "token",
        conversationId: "conversation-1",
        enabled: true
      })
    );

    await flushAsyncWork();

    expect(errorSpy).toHaveBeenCalledWith("StayFlow realtime connection failed to start.", expect.any(Error));
  });
});
