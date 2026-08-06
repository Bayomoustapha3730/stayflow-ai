import { useCallback, useEffect, useMemo, useState } from "react";
import { createAuthApi } from "../api/authApi";
import { ApiError, HttpClient } from "../api/httpClient";
import type { CurrentUserProfile } from "../models/organization";

const hostTokenStorageKey = "stayflow.host.accessToken";
const hostRefreshTokenStorageKey = "stayflow.host.refreshToken";

export interface UseHostAuthResult {
  accessToken: string | null;
  currentUser: CurrentUserProfile | null;
  isAuthenticated: boolean;
  isSigningIn: boolean;
  error: string | null;
  login: (email: string, password: string) => Promise<void>;
  logout: () => void;
  clearError: () => void;
  refreshCurrentUser: () => Promise<void>;
  setCurrentUserProfile: (profile: CurrentUserProfile | null) => void;
}

export function useHostAuth(): UseHostAuthResult {
  const [accessToken, setAccessToken] = useState<string | null>(() => sessionStorage.getItem(hostTokenStorageKey));
  const [, setRefreshToken] = useState<string | null>(() => sessionStorage.getItem(hostRefreshTokenStorageKey));
  const [currentUser, setCurrentUser] = useState<CurrentUserProfile | null>(null);
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

  const refreshCurrentUser = useCallback(async () => {
    if (!accessToken) {
      setCurrentUser(null);
      return;
    }

    const authenticatedHttp = new HttpClient({
      baseUrl: import.meta.env.VITE_STAYFLOW_API_URL ?? "http://localhost:5243",
      getAccessToken: () => accessToken
    });
    const authenticatedAuthApi = createAuthApi(authenticatedHttp);

    try {
      const profile = await authenticatedAuthApi.getCurrentUser();
      setCurrentUser(profile);
    } catch {
      setCurrentUser(null);
    }
  }, [accessToken]);

  useEffect(() => {
    void refreshCurrentUser();
  }, [refreshCurrentUser]);

  const login = useCallback(
    async (email: string, password: string) => {
      setError(null);
      setIsSigningIn(true);

      try {
        const response = await authApi.loginForDevelopment(email.trim(), password);
        setAccessToken(response.accessToken);
        setRefreshToken(response.refreshToken);
        sessionStorage.setItem(hostTokenStorageKey, response.accessToken);
        sessionStorage.setItem(hostRefreshTokenStorageKey, response.refreshToken);
      } catch (failure) {
        const message = failure instanceof Error ? failure.message : "Unable to sign in.";
        setError(message);
        setCurrentUser(null);
        throw failure;
      } finally {
        setIsSigningIn(false);
      }
    },
    [authApi]
  );

  const logout = useCallback(() => {
    setAccessToken(null);
    setRefreshToken(null);
    setCurrentUser(null);
    sessionStorage.removeItem(hostTokenStorageKey);
    sessionStorage.removeItem(hostRefreshTokenStorageKey);
    setError(null);
  }, []);

  const clearError = useCallback(() => setError(null), []);

  return {
    accessToken,
    currentUser,
    isAuthenticated: Boolean(accessToken),
    isSigningIn,
    error,
    login,
    logout,
    clearError,
    refreshCurrentUser,
    setCurrentUserProfile: setCurrentUser
  };
}

export function isHostSessionExpired(error: unknown): boolean {
  return error instanceof ApiError && error.status === 401;
}
