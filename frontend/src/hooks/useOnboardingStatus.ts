import { useCallback, useEffect, useMemo, useState } from "react";
import { createOnboardingApi } from "../api/onboardingApi";
import { HttpClient } from "../api/httpClient";
import { getRuntimeApiUrl } from "../runtimeConfig";
import type { OnboardingStatus } from "../models/onboarding";

export interface UseOnboardingStatusOptions {
  accessToken: string | null;
  /**
   * When provided (including null), status fetching waits for a known company id and any response
   * belonging to a different company is discarded. Omit this option entirely to fetch status for
   * whichever organization the access token is scoped to, without tenant-match gating.
   */
  activeCompanyId?: string | null;
}

export interface UseOnboardingStatusResult {
  status: OnboardingStatus | null;
  isLoading: boolean;
  isResolved: boolean;
  error: string | null;
  isIncomplete: boolean;
  refresh: () => Promise<OnboardingStatus | null>;
  /** Applies a status object already returned by a mutation, without an extra round trip. */
  applyStatus: (next: OnboardingStatus) => void;
}

/**
 * Canonical accessor for the server-authoritative onboarding status (GET /api/onboarding/status).
 * This is the single implementation all onboarding-aware UI (wizard, banners, route guards) must
 * consume instead of independently fetching or deriving onboarding completion state.
 */
export function useOnboardingStatus({ accessToken, activeCompanyId }: UseOnboardingStatusOptions): UseOnboardingStatusResult {
  const [status, setStatus] = useState<OnboardingStatus | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [isResolved, setIsResolved] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const hasCompanyGate = activeCompanyId !== undefined;
  const canFetch = Boolean(accessToken) && (!hasCompanyGate || Boolean(activeCompanyId));

  const http = useMemo(() => new HttpClient({
    baseUrl: getRuntimeApiUrl(),
    getAccessToken: () => accessToken
  }), [accessToken]);

  const api = useMemo(() => createOnboardingApi(http), [http]);

  const refresh = useCallback(async () => {
    if (!accessToken || (hasCompanyGate && !activeCompanyId)) {
      return null;
    }

    setIsLoading(true);
    setError(null);
    try {
      const next = await api.getStatus();
      if (hasCompanyGate && next.companyId !== activeCompanyId) {
        setIsResolved(true);
        return null;
      }

      setStatus(next);
      setIsResolved(true);
      return next;
    } catch (failure) {
      setError(failure instanceof Error ? failure.message : "Unable to load onboarding status.");
      setIsResolved(true);
      return null;
    } finally {
      setIsLoading(false);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [accessToken, activeCompanyId, hasCompanyGate, api]);

  useEffect(() => {
    setStatus(null);
    setIsResolved(false);
    setError(null);

    if (!canFetch) {
      return;
    }

    void refresh();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [accessToken, activeCompanyId, canFetch]);

  const applyStatus = useCallback((next: OnboardingStatus) => {
    setStatus(next);
    setIsResolved(true);
    setError(null);
  }, []);

  return {
    status,
    isLoading,
    isResolved,
    error,
    isIncomplete: status !== null && !status.isCompleted,
    refresh,
    applyStatus
  };
}
