import type { LoginResponse } from "../models/chat";
import type {
  AuthTokenSession,
  AuthSession,
  EmailVerificationRequest,
  PasswordResetConfirmRequest
} from "../models/auth";
import type {
  AuthorizedOrganizationSummary,
  ChangePasswordRequest,
  CreateOrganizationWorkspaceRequest,
  CurrentUserProfile,
  EmailVerificationChallenge,
  UpdateCurrentUserProfileRequest
} from "../models/organization";
import type { HttpClient } from "./httpClient";

export function createAuthApi(http: HttpClient) {
  return {
    loginForDevelopment(email: string, password: string) {
      return http.post<LoginResponse>("/auth/login", { email, password });
    },
    refreshSession(refreshToken: string) {
      return http.post<AuthTokenSession>("/auth/refresh", { refreshToken });
    },
    requestPasswordReset(email: string) {
      return http.post<Record<string, never>>("/auth/password-reset", { email });
    },
    confirmPasswordReset(request: PasswordResetConfirmRequest) {
      return http.post<Record<string, never>>("/auth/password-reset/confirm", request);
    },
    getCurrentUser() {
      return http.get<CurrentUserProfile>("/auth/me");
    },
    updateCurrentUser(request: UpdateCurrentUserProfileRequest) {
      return http.put<CurrentUserProfile>("/auth/me", request);
    },
    changePassword(request: ChangePasswordRequest) {
      return http.post<Record<string, never>>("/auth/change-password", request);
    },
    requestEmailVerification() {
      return http.post<EmailVerificationChallenge>("/auth/email-verification");
    },
    resendEmailVerification() {
      return http.post<EmailVerificationChallenge>("/auth/email-verification/resend");
    },
    confirmEmailVerification(request: EmailVerificationRequest) {
      return http.post<Record<string, never>>("/auth/email-verification/confirm", request);
    },
    listSessions() {
      return http.get<AuthSession[]>("/auth/sessions");
    },
    listOrganizations() {
      return http.get<AuthorizedOrganizationSummary[]>("/auth/organizations");
    },
    switchOrganization(companyId: string) {
      return http.post<AuthTokenSession>("/auth/organizations/switch", { companyId });
    },
    createOrganization(request: CreateOrganizationWorkspaceRequest) {
      return http.post<AuthTokenSession>("/auth/organizations", request);
    },
    revokeSession(sessionId: string) {
      return http.post<Record<string, never>>(`/auth/sessions/${sessionId}/revoke`);
    },
    revokeAllSessions() {
      return http.post<Record<string, never>>("/auth/sessions/revoke-all");
    }
  };
}
