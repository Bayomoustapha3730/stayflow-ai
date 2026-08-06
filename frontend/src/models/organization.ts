export interface CurrentUserProfile {
  id: string;
  companyId: string;
  fullName: string;
  email: string;
  phoneNumber: string;
  preferredLanguage: string;
  timeZone: string;
  isEmailVerified: boolean;
  emailNotificationsEnabled: boolean;
  securityNotificationsEnabled: boolean;
  productUpdatesEnabled: boolean;
  organizationRole?: string | null;
  roles: string[];
  permissions: string[];
}

export interface UpdateCurrentUserProfileRequest {
  fullName: string;
  phoneNumber: string;
  preferredLanguage: string;
  timeZone: string;
  emailNotificationsEnabled: boolean;
  securityNotificationsEnabled: boolean;
  productUpdatesEnabled: boolean;
}

export interface ChangePasswordRequest {
  currentPassword: string;
  newPassword: string;
}

export interface EmailVerificationChallenge {
  verificationToken: string;
  expiresAtUtc: string;
}

export interface OrganizationSummary {
  id: string;
  name: string;
  slug: string;
  status: string;
  ownerUserId?: string | null;
  brandingLogoUrl?: string | null;
  brandingPrimaryColor?: string | null;
  onboardingState?: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface OrganizationMember {
  userId: string;
  fullName: string;
  email: string;
  role: string;
  status: string;
  joinedAt: string;
  invitedByUserId?: string | null;
}

export interface UpdateOrganizationRequest {
  name: string;
  slug?: string;
  status?: string;
  brandingLogoUrl?: string;
  brandingPrimaryColor?: string;
  onboardingState?: string;
}