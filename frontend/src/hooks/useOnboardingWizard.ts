import { useCallback, useEffect, useMemo, useState } from "react";

function toCanonicalOnboardingPath(step: string | null | undefined): string | null {
  if (!step) {
    return null;
  }

  const normalized = step.toLowerCase();
  if (normalized.includes("complete")) {
    return "/get-started";
  }

  if (normalized.includes("organization")) {
    return "/onboarding/organization";
  }

  if (normalized.includes("plan")) {
    return "/onboarding/plan";
  }

  if (normalized.includes("property")) {
    return "/onboarding/property";
  }

  if (normalized.includes("team")) {
    return "/onboarding/team";
  }

  if (normalized.includes("whatsapp")) {
    return "/onboarding/whatsapp";
  }

  if (normalized.includes("ai")) {
    return "/onboarding/ai";
  }

  if (normalized.includes("knowledge")) {
    return "/onboarding/knowledge";
  }

  if (normalized.includes("demo")) {
    return "/onboarding/demo";
  }

  if (normalized.includes("review")) {
    return "/onboarding/review";
  }

  return "/onboarding/welcome";
}
import { createOnboardingApi } from "../api/onboardingApi";
import { ApiError, HttpClient } from "../api/httpClient";
import { getRuntimeApiUrl } from "../runtimeConfig";
import type {
  OnboardingAiProviderRequest,
  OnboardingDemoDataRequest,
  OnboardingInvitationsRequest,
  OnboardingKnowledgeRequest,
  OnboardingOrganizationRequest,
  OnboardingPlanRequest,
  OnboardingPropertyRequest,
  OnboardingStatus,
  OnboardingWhatsAppRequest
} from "../models/onboarding";

export interface UseOnboardingWizardOptions {
  accessToken: string | null;
  onUnauthorized?: () => void;
}

export function useOnboardingWizard({ accessToken, onUnauthorized }: UseOnboardingWizardOptions) {
  const [status, setStatus] = useState<OnboardingStatus | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [isSaving, setIsSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);

  const http = useMemo(() => new HttpClient({
    baseUrl: getRuntimeApiUrl(),
    getAccessToken: () => accessToken
  }), [accessToken]);

  const api = useMemo(() => createOnboardingApi(http), [http]);

  const handleFailure = useCallback((failure: unknown, fallback: string) => {
    if (failure instanceof ApiError && failure.status === 401) {
      onUnauthorized?.();
    }

    setError(failure instanceof Error ? failure.message : fallback);
  }, [onUnauthorized]);

  const refresh = useCallback(async () => {
    if (!accessToken) {
      setStatus(null);
      return;
    }

    setIsLoading(true);
    setError(null);
    try {
      const current = await api.getStatus();
      setStatus(current);
    } catch (failure) {
      handleFailure(failure, "Unable to load onboarding status.");
    } finally {
      setIsLoading(false);
    }
  }, [accessToken, api, handleFailure]);

  useEffect(() => {
    void refresh();
  }, [refresh]);

  useEffect(() => {
    const canonicalPath = toCanonicalOnboardingPath(status?.currentStep);
    if (!canonicalPath) {
      return;
    }

    const currentPath = window.location.pathname.toLowerCase();
    if (currentPath === canonicalPath) {
      return;
    }

    if (currentPath === "/onboarding" || currentPath === "/onboarding/" || currentPath.startsWith("/onboarding/") || currentPath === "/get-started" || currentPath === "/get-started/") {
      window.history.replaceState({}, "", canonicalPath);
    }
  }, [status?.currentStep]);

  const withStatusMutation = useCallback(async (action: () => Promise<OnboardingStatus>, successMessage: string) => {
    if (!accessToken) {
      return null;
    }

    setIsSaving(true);
    setError(null);
    setMessage(null);

    try {
      const next = await action();
      setStatus(next);
      setMessage(successMessage);
      return next;
    } catch (failure) {
      handleFailure(failure, "Onboarding update failed.");
      return null;
    } finally {
      setIsSaving(false);
    }
  }, [accessToken, handleFailure]);

  const start = useCallback(async () => {
    return withStatusMutation(() => api.start(), "Onboarding started.");
  }, [api, withStatusMutation]);

  const saveOrganization = useCallback(async (request: OnboardingOrganizationRequest) => {
    return withStatusMutation(() => api.completeOrganization(request), "Organization profile saved.");
  }, [api, withStatusMutation]);

  const confirmPlan = useCallback(async (request: OnboardingPlanRequest) => {
    return withStatusMutation(() => api.completePlan(request), "Plan confirmed.");
  }, [api, withStatusMutation]);

  const createProperty = useCallback(async (request: OnboardingPropertyRequest) => {
    return withStatusMutation(() => api.completeProperty(request), "Property configured.");
  }, [api, withStatusMutation]);

  const submitInvitations = useCallback(async (request: OnboardingInvitationsRequest) => {
    if (!accessToken) {
      return null;
    }

    setIsSaving(true);
    setError(null);
    setMessage(null);
    try {
      const response = await api.completeInvitations(request);
      setStatus(response.status);
      setMessage("Invitation step updated.");
      return response;
    } catch (failure) {
      handleFailure(failure, "Invitations submission failed.");
      return null;
    } finally {
      setIsSaving(false);
    }
  }, [accessToken, api, handleFailure]);

  const configureWhatsApp = useCallback(async (request: OnboardingWhatsAppRequest) => {
    return withStatusMutation(() => api.completeWhatsApp(request), "WhatsApp readiness confirmed.");
  }, [api, withStatusMutation]);

  const configureAi = useCallback(async (request: OnboardingAiProviderRequest) => {
    return withStatusMutation(() => api.completeAiProvider(request), "AI provider readiness confirmed.");
  }, [api, withStatusMutation]);

  const submitKnowledge = useCallback(async (request: OnboardingKnowledgeRequest) => {
    return withStatusMutation(() => api.completeKnowledge(request), "Knowledge setup complete.");
  }, [api, withStatusMutation]);

  const generateDemoData = useCallback(async (request: OnboardingDemoDataRequest) => {
    return withStatusMutation(() => api.completeDemoData(request), "Demo data completed.");
  }, [api, withStatusMutation]);

  const skipStep = useCallback(async (step: string, reason?: string) => {
    return withStatusMutation(() => api.skipStep(step, { reason }), `Step ${step} skipped.`);
  }, [api, withStatusMutation]);

  const complete = useCallback(async () => {
    return withStatusMutation(() => api.complete({ confirmChecklistReviewed: true }), "Onboarding completed.");
  }, [api, withStatusMutation]);

  return {
    status,
    isLoading,
    isSaving,
    error,
    message,
    refresh,
    start,
    saveOrganization,
    confirmPlan,
    createProperty,
    submitInvitations,
    configureWhatsApp,
    configureAi,
    submitKnowledge,
    generateDemoData,
    skipStep,
    complete
  };
}
