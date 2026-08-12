import { useEffect, useMemo, useState } from "react";
import { createOnboardingApi } from "../api/onboardingApi";
import { HttpClient } from "../api/httpClient";
import { getRuntimeApiUrl } from "../runtimeConfig";
import type { OnboardingStatus } from "../models/onboarding";

interface UseOrganizationOnboardingStatusOptions {
  accessToken: string | null;
  activeCompanyId: string | null;
}

export interface UseOrganizationOnboardingStatusResult {
  status: OnboardingStatus | null;
  isResolved: boolean;
  isIncomplete: boolean;
}

/**
 * Resolves onboarding status for the active organization only; status from any other
 * organization is discarded so tenant state can never leak across an organization switch.
 */
export function useOrganizationOnboardingStatus({
  accessToken,
  activeCompanyId
}: UseOrganizationOnboardingStatusOptions): UseOrganizationOnboardingStatusResult {
  const [status, setStatus] = useState<OnboardingStatus | null>(null);
  const [isResolved, setIsResolved] = useState(false);

  const api = useMemo(() => {
    if (!accessToken) {
      return null;
    }

    return createOnboardingApi(new HttpClient({
      baseUrl: getRuntimeApiUrl(),
      getAccessToken: () => accessToken
    }));
  }, [accessToken]);

  useEffect(() => {
    setStatus(null);
    setIsResolved(false);

    // The active organization must be known before any status can be attributed to it.
    if (!api || !activeCompanyId) {
      return;
    }

    let cancelled = false;

    void api.getStatus()
      .then((next) => {
        if (cancelled || next.companyId !== activeCompanyId) {
          return;
        }

        setStatus(next);
        setIsResolved(true);
      })
      .catch(() => {
        if (!cancelled) {
          setIsResolved(true);
        }
      });

    return () => {
      cancelled = true;
    };
  }, [activeCompanyId, api]);

  return {
    status,
    isResolved,
    isIncomplete: status !== null && !status.isCompleted
  };
}
