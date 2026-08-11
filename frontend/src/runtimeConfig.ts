export interface StayFlowRuntimeConfig {
  apiUrl?: string;
  signalRUrl?: string;
  environment?: string;
}

declare global {
  interface Window {
    __STAYFLOW_RUNTIME_CONFIG__?: StayFlowRuntimeConfig;
  }
}

export function getRuntimeApiUrl(): string {
  return resolveRuntimeApiUrl(
    window.__STAYFLOW_RUNTIME_CONFIG__?.apiUrl,
    import.meta.env.VITE_STAYFLOW_API_URL,
    window.location.origin
  );
}

export function getRuntimeSignalRUrl(): string | undefined {
  return resolveRuntimeSignalRUrl(
    window.__STAYFLOW_RUNTIME_CONFIG__?.signalRUrl,
    import.meta.env.VITE_STAYFLOW_SIGNALR_URL,
    window.location.origin
  );
}

export function getRuntimeEnvironment(): string {
  return window.__STAYFLOW_RUNTIME_CONFIG__?.environment ?? import.meta.env.MODE ?? "development";
}

export function resolveRuntimeApiUrl(runtimeApiUrl: string | null | undefined, envApiUrl: string | null | undefined, origin: string): string {
  if (runtimeApiUrl?.trim()) {
    return runtimeApiUrl.trim().replace(/\/$/, "");
  }

  if (envApiUrl?.trim()) {
    return envApiUrl.trim().replace(/\/$/, "");
  }

  return origin.replace(/\/$/, "");
}

export function resolveRuntimeSignalRUrl(
  runtimeSignalRUrl: string | null | undefined,
  envSignalRUrl: string | null | undefined,
  origin: string
): string {
  if (runtimeSignalRUrl?.trim()) {
    return runtimeSignalRUrl.trim().replace(/\/$/, "");
  }

  if (envSignalRUrl?.trim()) {
    return envSignalRUrl.trim().replace(/\/$/, "");
  }

  return `${origin.replace(/\/$/, "")}/hubs/conversations`;
}
