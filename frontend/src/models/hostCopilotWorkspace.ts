export interface HostCopilotWorkspaceResponse {
  generatedAt: string;
  totalOpenItems: number;
  totalBreachedSlaItems: number;
  items: HostCopilotWorkItem[];
}

export interface HostCopilotWorkItem {
  workItemId: string;
  conversationId: string;
  propertyId: string;
  reservationId?: string | null;
  propertyName: string;
  guestName: string;
  priority: "Low" | "Normal" | "High" | "Urgent" | string;
  isEmergency: boolean;
  safetyClassification: string;
  priorityReason: string;
  sla: HostCopilotSlaStatus;
  summary: HostCopilotOperationalSummary;
  timeline: HostCopilotTimelineEvent[];
  recommendations: HostCopilotRecommendation[];
  pendingActions: HostCopilotPendingAction[];
}

export interface HostCopilotSlaStatus {
  minutesSinceLatestGuestMessage: number;
  responseDueAt?: string | null;
  isBreached: boolean;
  alertLevel: string;
  alertMessage: string;
}

export interface HostCopilotOperationalSummary {
  headline: string;
  lastGuestIntent: string;
  lastGuestMessagePreview: string;
  openActionCount: number;
  visibleMessageCount: number;
  lastActivityAt: string;
}

export interface HostCopilotTimelineEvent {
  timestamp: string;
  eventType: string;
  title: string;
  detail: string;
}

export interface HostCopilotRecommendation {
  code: string;
  title: string;
  reason: string;
  suggestedAction: string;
  confidence: number;
}

export interface HostCopilotPendingAction {
  actionId: string;
  actionType: string;
  status: string;
  createdAt: string;
  expiresAt: string;
}

export interface HostCopilotDraftGenerateRequest {
  tone?: string;
  hostInstruction?: string;
}

export interface HostCopilotDraftValidateRequest {
  draft: string;
}

export interface HostCopilotDraftSendRequest {
  draft: string;
}

export interface HostCopilotDraftValidationResponse {
  isValid: boolean;
  errors: string[];
  warnings: string[];
}

export interface HostCopilotDraftResponse {
  conversationId: string;
  draft: string;
  usedDeterministicFallback: boolean;
  generationMode: string;
  rationale: string;
  validation: HostCopilotDraftValidationResponse;
}
