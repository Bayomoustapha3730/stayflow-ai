export interface PasswordResetRequest {
  email: string;
}

export interface PasswordResetConfirmRequest {
  token: string;
  newPassword: string;
}

export interface EmailVerificationRequest {
  token: string;
}

export interface AuthSession {
  sessionId: string;
  createdAtUtc: string;
  lastUsedAtUtc?: string | null;
  expiresAtUtc: string;
  isCurrent: boolean;
  ipAddress?: string | null;
  userAgent?: string | null;
}

export interface AuthTokenSession {
  accessToken: string;
  refreshToken: string;
  sessionId?: string;
  expiresAt: string;
}

export interface InvitationDecisionRequest {
  token: string;
}