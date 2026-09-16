import { getRuntimeApiUrl } from "../runtimeConfig";
import { useCallback, useContext, useEffect, useMemo, useRef, useState } from "react";
import { createAuthApi } from "../api/authApi";
import { ApiError, HttpClient } from "../api/httpClient";
import { createOnboardingApi } from "../api/onboardingApi";
import { HostAuthContext } from "../context/HostAuthContext";
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

/**
 * Owns the single, shared host-auth runtime (token state, current user, organization
 * switching). This must only ever be instantiated once, by HostAuthProvider — consumers
 * should import `useHostAuth` from this module, which reads from that shared instance.
 */
export function useHostAuthState(): UseHostAuthResult {
  const [accessToken, setAccessToken] = useState<string | null>(() => sessionStorage.getItem(hostTokenStorageKey));
  const [refreshToken, setRefreshToken] = useState<string | null>(() => sessionStorage.getItem(hostRefreshTokenStorageKey));
  const [currentUser, setCurrentUser] = useState<CurrentUserProfile | null>(null);
  const [isSigningIn, setIsSigningIn] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const isSwitchingRef = useRef(false);

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

    // /auth/me already reports the live, authoritative CompanyId for this user; it must
    // never be second-guessed or silently replaced by auto-selecting a different
    // organization as a side effect of loading a page. If it is missing entirely, fail
    // closed rather than guessing an organization.
    if (!profile.companyId) {
      persistSession(null);
      setCurrentUser(null);
      setError("No active organization membership is available for this account.");
      return null;
    }

    setCurrentUser(profile);
    return profile;
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
    if (!accessToken || isSwitchingRef.current) {
      return false;
    }

    isSwitchingRef.current = true;
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
    } finally {
      isSwitchingRef.current = false;
    }
  }, [accessToken, buildAuthenticatedApi, loadCurrentUserWithToken, navigateToCurrentOnboardingStepIfRequired, persistSession]);

  const createOrganization = useCallback(async (request: CreateOrganizationWorkspaceRequest) => {
    if (!accessToken || isSwitchingRef.current) {
      return false;
    }

    isSwitchingRef.current = true;
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
    } finally {
      isSwitchingRef.current = false;
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

/**
 * Consumes the single shared HostAuthProvider instance. Every component that needs host
 * auth/organization state must call this instead of instantiating its own runtime.
 */
export function useHostAuth(): UseHostAuthResult {
  const context = useContext(HostAuthContext);
  if (!context) {
    throw new Error("useHostAuth must be used within a HostAuthProvider");
  }

  return context;
}

export function isHostSessionExpired(error: unknown): boolean {
  return error instanceof ApiError && error.status === 401;
}
