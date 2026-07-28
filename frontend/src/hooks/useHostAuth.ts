import { useCallback, useMemo, useState } from "react";
import { createAuthApi } from "../api/authApi";
import { ApiError, HttpClient } from "../api/httpClient";

const hostTokenStorageKey = "stayflow.host.accessToken";

export interface UseHostAuthResult {
  accessToken: string | null;
  isAuthenticated: boolean;
  isSigningIn: boolean;
  error: string | null;
  login: (email: string, password: string) => Promise<void>;
  logout: () => void;
  clearError: () => void;
}

export function useHostAuth(): UseHostAuthResult {
  const [accessToken, setAccessToken] = useState<string | null>(() => sessionStorage.getItem(hostTokenStorageKey));
  const [isSigningIn, setIsSigningIn] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const http = useMemo(
    () =>
      new HttpClient({
        baseUrl: import.meta.env.VITE_STAYFLOW_API_URL ?? "http://localhost:5243"
      }),
    []
  );

  const authApi = useMemo(() => createAuthApi(http), [http]);

  const login = useCallback(
    async (email: string, password: string) => {
      setError(null);
      setIsSigningIn(true);

      try {
        const response = await authApi.loginForDevelopment(email.trim(), password);
        setAccessToken(response.accessToken);
        sessionStorage.setItem(hostTokenStorageKey, response.accessToken);
      } catch (failure) {
        const message = failure instanceof Error ? failure.message : "Unable to sign in.";
        setError(message);
        throw failure;
      } finally {
        setIsSigningIn(false);
      }
    },
    [authApi]
  );

  const logout = useCallback(() => {
    setAccessToken(null);
    sessionStorage.removeItem(hostTokenStorageKey);
    setError(null);
  }, []);

  const clearError = useCallback(() => setError(null), []);

  return {
    accessToken,
    isAuthenticated: Boolean(accessToken),
    isSigningIn,
    error,
    login,
    logout,
    clearError
  };
}

export function isHostSessionExpired(error: unknown): boolean {
  return error instanceof ApiError && error.status === 401;
}
