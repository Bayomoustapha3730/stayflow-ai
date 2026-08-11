import { afterEach, describe, expect, it, vi } from "vitest";
import configScript from "../public/config.js?raw";
import {
  resolveRuntimeApiUrl,
  resolveRuntimeSignalRUrl
} from "../src/runtimeConfig";

async function executeConfigScript(existingConfig?: Record<string, unknown>, origin = "https://frontend.example.test:5173") {
  const sandboxWindow = {
    location: { origin },
    __STAYFLOW_RUNTIME_CONFIG__: existingConfig
  } as {
    location: { origin: string };
    __STAYFLOW_RUNTIME_CONFIG__?: Record<string, unknown>;
  };

  new Function("window", configScript)(sandboxWindow);
  return sandboxWindow.__STAYFLOW_RUNTIME_CONFIG__;
}

afterEach(() => {
  vi.unstubAllEnvs();
});

describe("runtime config precedence", () => {
  it("prefers explicit runtime API config over env and origin", () => {
    expect(
      resolveRuntimeApiUrl(
        "https://runtime-backend.example/api",
        "https://env-backend.example/api",
        "https://frontend.example.test:5173"
      )
    ).toBe("https://runtime-backend.example/api");

    expect(
      resolveRuntimeSignalRUrl(
        "https://runtime-backend.example/hubs/conversations",
        "https://env-backend.example/hubs/conversations",
        "https://frontend.example.test:5173"
      )
    ).toBe("https://runtime-backend.example/hubs/conversations");
  });

  it("uses VITE env values when explicit runtime config is missing", () => {
    expect(
      resolveRuntimeApiUrl(
        undefined,
        "https://env-backend.example/api",
        "https://frontend.example.test:5173"
      )
    ).toBe("https://env-backend.example/api");

    expect(
      resolveRuntimeSignalRUrl(
        undefined,
        "https://env-backend.example/hubs/conversations",
        "https://frontend.example.test:5173"
      )
    ).toBe("https://env-backend.example/hubs/conversations");
  });

  it("falls back to same-origin only after runtime and env values are absent", () => {
    expect(
      resolveRuntimeApiUrl(
        undefined,
        undefined,
        "https://frontend.example.test:5173"
      )
    ).toBe("https://frontend.example.test:5173");

    expect(
      resolveRuntimeSignalRUrl(
        undefined,
        undefined,
        "https://frontend.example.test:5173"
      )
    ).toBe("https://frontend.example.test:5173/hubs/conversations");
  });

  it("does not let the frontend origin override a configured backend URL", async () => {
    const runtimeConfig = await executeConfigScript(undefined, "https://frontend-5173.codespaces.example");

    expect(runtimeConfig).toEqual({});
    expect(
      resolveRuntimeApiUrl(
        runtimeConfig?.apiUrl as string | undefined,
        "https://backend-5243.codespaces.example",
        "https://frontend-5173.codespaces.example"
      )
    ).toBe("https://backend-5243.codespaces.example");

    expect(
      resolveRuntimeSignalRUrl(
        runtimeConfig?.signalRUrl as string | undefined,
        "https://backend-5243.codespaces.example/hubs/conversations",
        "https://frontend-5173.codespaces.example"
      )
    ).toBe("https://backend-5243.codespaces.example/hubs/conversations");
  });
});
