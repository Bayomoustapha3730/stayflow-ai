export interface CurrentUserProfile {
  id: string;
  companyId: string;
  fullName: string;
  email: string;
  phoneNumber: string;
  isEmailVerified: boolean;
  organizationRole?: string | null;
  roles: string[];
  permissions: string[];
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