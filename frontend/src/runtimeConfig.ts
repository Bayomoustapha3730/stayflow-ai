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
  const runtimeConfig = window.__STAYFLOW_RUNTIME_CONFIG__;
  if (runtimeConfig?.apiUrl) {
    return runtimeConfig.apiUrl;
  }

  return import.meta.env.VITE_STAYFLOW_API_URL || "http://localhost:5243";
}

export function getRuntimeSignalRUrl(): string | undefined {
  return window.__STAYFLOW_RUNTIME_CONFIG__?.signalRUrl ?? import.meta.env.VITE_STAYFLOW_SIGNALR_URL;
}

export function getRuntimeEnvironment(): string {
  return window.__STAYFLOW_RUNTIME_CONFIG__?.environment ?? import.meta.env.MODE ?? "development";
}
