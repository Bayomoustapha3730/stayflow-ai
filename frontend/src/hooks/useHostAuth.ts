import { getRuntimeApiUrl } from "../runtimeConfig";
import { useCallback, useEffect, useMemo, useState } from "react";
import { createAuthApi } from "../api/authApi";
import { ApiError, HttpClient } from "../api/httpClient";
import { createOnboardingApi } from "../api/onboardingApi";
import type { AuthTokenSession } from "../models/auth";
import type {
  CreateOrganizationWorkspaceRequest,
  CurrentUserProfile
} from "../models/organization";

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
  switchOrganization: (companyId: string) => Promise<boolean>;
  createOrganization: (request: CreateOrganizationWorkspaceRequest) => Promise<boolean>;
  setCurrentUserProfile: (profile: CurrentUserProfile | null) => void;
}

export function useHostAuth(): UseHostAuthResult {
  const [accessToken, setAccessToken] = useState<string | null>(() => sessionStorage.getItem(hostTokenStorageKey));
  const [refreshToken, setRefreshToken] = useState<string | null>(() => sessionStorage.getItem(hostRefreshTokenStorageKey));
  const [currentUser, setCurrentUser] = useState<CurrentUserProfile | null>(null);
  const [isSigningIn, setIsSigningIn] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const http = useMemo(
    () =>
      new HttpClient({
        baseUrl: getRuntimeApiUrl()
      }),
    []
  );

  const authApi = useMemo(() => createAuthApi(http), [http]);

  const persistSession = useCallback((session: AuthTokenSession | null) => {
    if (!session) {
      setAccessToken(null);
      setRefreshToken(null);
      sessionStorage.removeItem(hostTokenStorageKey);
      sessionStorage.removeItem(hostRefreshTokenStorageKey);
      return;
    }

    setAccessToken(session.accessToken);
    setRefreshToken(session.refreshToken);
    sessionStorage.setItem(hostTokenStorageKey, session.accessToken);
    sessionStorage.setItem(hostRefreshTokenStorageKey, session.refreshToken);
  }, []);

  const buildAuthenticatedApi = useCallback((token: string) => {
    const authenticatedHttp = new HttpClient({
      baseUrl: getRuntimeApiUrl(),
      getAccessToken: () => token
    });

    return {
      auth: createAuthApi(authenticatedHttp),
      onboarding: createOnboardingApi(authenticatedHttp)
    };
  }, []);

  const navigateToCurrentOnboardingStepIfRequired = useCallback(async (token: string) => {
    try {
      const { onboarding } = buildAuthenticatedApi(token);
      const status = await onboarding.getStatus().catch(() => onboarding.start());
      if (status.isCompleted) {
        return;
      }

      const targetPath = status.safeLinks?.find((item) => item.rel === "current_step")?.href ?? "/onboarding";
      if (targetPath.startsWith("/") && window.location.pathname !== targetPath) {
        window.history.pushState({}, "", targetPath);
        window.dispatchEvent(new PopStateEvent("popstate"));
      }
    } catch {
      // Auth state remains valid even if onboarding lookup fails.
    }
  }, [buildAuthenticatedApi]);

  const loadCurrentUserWithToken = useCallback(async (token: string) => {
    const { auth } = buildAuthenticatedApi(token);
    const profile = await auth.getCurrentUser();
    const organizations = await auth.listOrganizations();

    if (organizations.some((item) => item.companyId === profile.companyId && item.membershipStatus === "Active")) {
      setCurrentUser(profile);
      return profile;
    }

    const fallbackOrganization = organizations[0];
    if (!fallbackOrganization) {
      persistSession(null);
      setCurrentUser(null);
      setError("No active organization membership is available for this account.");
      return null;
    }

    const fallbackSession = await auth.switchOrganization(fallbackOrganization.companyId);
    persistSession(fallbackSession);
    const fallbackProfile = await buildAuthenticatedApi(fallbackSession.accessToken).auth.getCurrentUser();
    setCurrentUser(fallbackProfile);
    return fallbackProfile;
  }, [buildAuthenticatedApi, persistSession]);

  const refreshSession = useCallback(async () => {
    if (!refreshToken) {
      return null;
    }

    const session = await authApi.refreshSession(refreshToken);
    persistSession(session);
    return session.accessToken;
  }, [authApi, persistSession, refreshToken]);

  const refreshCurrentUser = useCallback(async () => {
    if (!accessToken) {
      setCurrentUser(null);
      return;
    }

    try {
      await loadCurrentUserWithToken(accessToken);
    } catch (failure) {
      if (failure instanceof ApiError && failure.status === 401) {
        try {
          const nextToken = await refreshSession();
          if (nextToken) {
            await loadCurrentUserWithToken(nextToken);
            return;
          }
        } catch {
          // Fall through to clearing auth state.
        }

        persistSession(null);
        setCurrentUser(null);
        return;
      }

      setCurrentUser(null);
    }
  }, [accessToken, loadCurrentUserWithToken, persistSession, refreshSession]);

  useEffect(() => {
    void refreshCurrentUser();
  }, [refreshCurrentUser]);

  const login = useCallback(
    async (email: string, password: string) => {
      setError(null);
      setIsSigningIn(true);

      try {
        const response = await authApi.loginForDevelopment(email.trim(), password);
        persistSession(response);
        await loadCurrentUserWithToken(response.accessToken);
      } catch (failure) {
        const message = failure instanceof Error ? failure.message : "Unable to sign in.";
        setError(message);
        setCurrentUser(null);
        throw failure;
      } finally {
        setIsSigningIn(false);
      }
    },
    [authApi, loadCurrentUserWithToken, persistSession]
  );

  const logout = useCallback(() => {
    persistSession(null);
    setCurrentUser(null);
    setError(null);
  }, [persistSession]);

  const switchOrganization = useCallback(async (companyId: string) => {
    if (!accessToken) {
      return false;
    }

    setError(null);

    try {
      const { auth } = buildAuthenticatedApi(accessToken);
      const session = await auth.switchOrganization(companyId);
      persistSession(session);
      const profile = await loadCurrentUserWithToken(session.accessToken);
      if (!profile) {
        return false;
      }

      await navigateToCurrentOnboardingStepIfRequired(session.accessToken);
      return true;
    } catch (failure) {
      setError(failure instanceof Error ? failure.message : "Unable to switch organization.");
      return false;
    }
  }, [accessToken, buildAuthenticatedApi, loadCurrentUserWithToken, navigateToCurrentOnboardingStepIfRequired, persistSession]);

  const createOrganization = useCallback(async (request: CreateOrganizationWorkspaceRequest) => {
    if (!accessToken) {
      return false;
    }

    setError(null);

    try {
      const { auth } = buildAuthenticatedApi(accessToken);
      const session = await auth.createOrganization(request);
      persistSession(session);
      const profile = await loadCurrentUserWithToken(session.accessToken);
      if (!profile) {
        return false;
      }

      await navigateToCurrentOnboardingStepIfRequired(session.accessToken);
      return true;
    } catch (failure) {
      setError(failure instanceof Error ? failure.message : "Unable to create organization.");
      return false;
    }
  }, [accessToken, buildAuthenticatedApi, loadCurrentUserWithToken, navigateToCurrentOnboardingStepIfRequired, persistSession]);

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
    switchOrganization,
    createOrganization,
    setCurrentUserProfile: setCurrentUser
  };
}

export function isHostSessionExpired(error: unknown): boolean {
  return error instanceof ApiError && error.status === 401;
}
