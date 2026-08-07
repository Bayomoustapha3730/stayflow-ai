import type { HttpClient } from "./httpClient";
import type {
  OnboardingActionResponse,
  OnboardingAiProviderRequest,
  OnboardingCompleteRequest,
  OnboardingDemoDataRequest,
  OnboardingInvitationsRequest,
  OnboardingInvitationsResponse,
  OnboardingKnowledgeRequest,
  OnboardingOrganizationRequest,
  OnboardingPlanRequest,
  OnboardingPropertyRequest,
  OnboardingResetRequest,
  OnboardingSkipStepRequest,
  OnboardingStatus,
  OnboardingWhatsAppRequest
} from "../models/onboarding";

export function createOnboardingApi(http: HttpClient) {
  return {
    getStatus() {
      return http.get<OnboardingStatus>("/api/onboarding/status");
    },
    start() {
      return http.post<OnboardingStatus>("/api/onboarding/start");
    },
    completeOrganization(request: OnboardingOrganizationRequest) {
      return http.post<OnboardingStatus>("/api/onboarding/organization", request);
    },
    completePlan(request: OnboardingPlanRequest) {
      return http.post<OnboardingStatus>("/api/onboarding/plan", request);
    },
    completeProperty(request: OnboardingPropertyRequest) {
      return http.post<OnboardingStatus>("/api/onboarding/property", request);
    },
    completeInvitations(request: OnboardingInvitationsRequest) {
      return http.post<OnboardingActionResponse<OnboardingInvitationsResponse>>("/api/onboarding/invitations", request);
    },
    completeWhatsApp(request: OnboardingWhatsAppRequest) {
      return http.post<OnboardingStatus>("/api/onboarding/whatsapp", request);
    },
    completeAiProvider(request: OnboardingAiProviderRequest) {
      return http.post<OnboardingStatus>("/api/onboarding/ai-provider", request);
    },
    completeKnowledge(request: OnboardingKnowledgeRequest) {
      return http.post<OnboardingStatus>("/api/onboarding/knowledge", request);
    },
    completeDemoData(request: OnboardingDemoDataRequest) {
      return http.post<OnboardingStatus>("/api/onboarding/demo-data", request);
    },
    skipStep(step: string, request: OnboardingSkipStepRequest) {
      return http.post<OnboardingStatus>(`/api/onboarding/steps/${encodeURIComponent(step)}/skip`, request);
    },
    complete(request: OnboardingCompleteRequest) {
      return http.post<OnboardingStatus>("/api/onboarding/complete", request);
    },
    reset(request: OnboardingResetRequest) {
      return http.post<OnboardingStatus>("/api/onboarding/reset", request);
    }
  };
}
