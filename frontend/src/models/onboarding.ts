export type OnboardingStepState = "NotStarted" | "InProgress" | "Completed" | "Skipped" | "Blocked";

export interface OnboardingBlocker {
  step: string;
  code: string;
  message: string;
}

export interface OnboardingChecklistItem {
  key: string;
  status: string;
  optional: boolean;
  recommendation: string;
}

export interface OnboardingSafeLink {
  rel: string;
  href: string;
}

export interface OnboardingStatus {
  companyId: string;
  userId: string;
  currentStep: string;
  currentStepState: OnboardingStepState | string;
  completedSteps: string[];
  remainingSteps: string[];
  skippedSteps: string[];
  blockers: OnboardingBlocker[];
  checklist: OnboardingChecklistItem[];
  percentComplete: number;
  nextRecommendedAction?: string | null;
  safeLinks: OnboardingSafeLink[];
  startedAtUtc: string;
  selectedPlanName?: string | null;
  firstPropertyId?: string | null;
  isCompleted: boolean;
  completedAtUtc?: string | null;
  completedByUserId?: string | null;
  lastUpdatedAtUtc: string;
  version: number;
}

export interface OnboardingInvitationInput {
  email: string;
  role: string;
}

export interface OnboardingInvitationResult {
  email: string;
  role: string;
  success: boolean;
  message: string;
}

export interface OnboardingInvitationsResponse {
  results: OnboardingInvitationResult[];
}

export interface OnboardingActionResponse<T> {
  status: OnboardingStatus;
  result?: T;
}

export interface OnboardingOrganizationRequest {
  name: string;
  slug?: string;
  supportContactEmail?: string;
  timeZone?: string;
  locale?: string;
  brandingLogoUrl?: string;
  brandingPrimaryColor?: string;
}

export interface OnboardingPlanRequest {
  planName?: string;
}

export interface OnboardingPropertyRequest {
  name: string;
  addressLine1: string;
  addressLine2?: string;
  city: string;
  countryCode: string;
  timeZone: string;
  description?: string;
  idempotencyKey?: string;
}

export interface OnboardingInvitationsRequest {
  invitations: OnboardingInvitationInput[];
}

export interface OnboardingWhatsAppRequest {
  integrationId?: string;
  runHealthCheck: boolean;
}

export interface OnboardingAiProviderRequest {
  acknowledgeDeterministicFallback: boolean;
  skipIfDeterministicOnly: boolean;
}

export interface OnboardingKnowledgeRequest {
  propertyId?: string;
  title: string;
  content: string;
  summary?: string;
  tags: string[];
  idempotencyKey?: string;
}

export interface OnboardingDemoDataRequest {
  createSampleKnowledge: boolean;
  createSampleReservation: boolean;
  createSampleConversation: boolean;
  createSampleHostCopilotItem: boolean;
  idempotencyKey?: string;
}

export interface OnboardingSkipStepRequest {
  reason?: string;
}

export interface OnboardingCompleteRequest {
  confirmChecklistReviewed: boolean;
}

export interface OnboardingResetRequest {
  confirm: boolean;
}
