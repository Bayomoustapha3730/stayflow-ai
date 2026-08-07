export interface PlatformTenantSummary {
  companyId: string;
  name: string;
  status: string;
  subscriptionStatus?: string | null;
  userCount: number;
  propertyCount: number;
  createdAt: string;
}

export interface PlatformTenantPagedResult {
  items: PlatformTenantSummary[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface PlatformTenantDetail {
  companyId: string;
  name: string;
  slug: string;
  status: string;
  isActive: boolean;
  subscriptionStatus?: string | null;
  currentPlanName?: string | null;
  userCount: number;
  propertyCount: number;
  conversationCount: number;
  aiUsageLast30Days: number;
  apiUsageLast30Days: number;
  createdAt: string;
  updatedAt: string;
}

export interface PlatformOrganizationHealth {
  companyId: string;
  organizationName: string;
  status: string;
  isActive: boolean;
  activeUserCount: number;
  activeOwnerOrAdminCount: number;
  activePropertyCount: number;
  openConversations: number;
  overdueActionCount: number;
  hasBlockingIssues: boolean;
  healthSignals: string[];
}

export interface PlatformUsageOverview {
  apiRequestsLast30Days: number;
  aiRequestsLast30Days: number;
  aiTokensLast30Days: number;
  whatsAppMessagesLast30Days: number;
  reservationsLast30Days: number;
  fileUploadsLast30Days: number;
  generatedAtUtc: string;
}

export interface PlatformFeatureFlag {
  planName: string;
  planId: string;
  key: string;
  isEnabled: boolean;
  isUnlimited: boolean;
  quotaLimit?: number | null;
  unit?: string | null;
  notes?: string | null;
}

export interface PlatformOperationalMetrics {
  apiRequestsLast30Days: number;
  signalREventsLast30Days: number;
  aiRequestsLast30Days: number;
  billingWebhookEventsLast30Days: number;
  emailEventsLast30Days: number;
  whatsAppMessagesLast30Days: number;
  backgroundJobRetriesLast30Days: number;
  databaseHealthScore: number;
  queueDepthEstimate: number;
  healthCheckIssuesLast24Hours: number;
  generatedAtUtc: string;
}

export interface PlatformBackgroundJobStatus {
  jobName: string;
  status: string;
  lastObservedAtUtc?: string | null;
  failureCountLast24Hours: number;
}

export interface PlatformWebhookMonitoring {
  provider: string;
  totalEventsLast24Hours: number;
  duplicatesLast24Hours: number;
  failedInvoiceEventsLast24Hours: number;
  latestProcessedAtUtc?: string | null;
}

export interface PlatformQueueMonitoring {
  queueName: string;
  depthEstimate: number;
  notes: string;
}

export interface PlatformEmailDeliveryMonitoring {
  passwordResetIssuedLast24Hours: number;
  emailVerificationIssuedLast24Hours: number;
  expiredEmailTokensLast24Hours: number;
  generatedAtUtc: string;
}

export interface PlatformProviderHealth {
  provider: string;
  status: string;
  message: string;
  checkedAtUtc: string;
}

export interface PlatformBillingHealth {
  activeSubscriptions: number;
  pastDueSubscriptions: number;
  failedInvoicesLast30Days: number;
  webhookEventsLast24Hours: number;
  generatedAtUtc: string;
}

export interface PlatformReadOnlyDiagnostic {
  area: string;
  key: string;
  value: string;
}

export interface PlatformSystemConfiguration {
  environmentName: string;
  aiProvider: string;
  openAiConfigured: boolean;
  authRateLimitPerMinute: number;
  hostApiRateLimitPerMinute: number;
  aiGenerationRateLimitPerMinute: number;
  billingWebhookEnabled: boolean;
  whatsAppWebhookEnabled: boolean;
}

export interface PlatformTenantLifecycleAudit {
  auditLogId: string;
  action: string;
  details: string;
  createdAt: string;
}

export interface PlatformSupportImpersonationSession {
  sessionId: string;
  targetCompanyId: string;
  targetUserId: string;
  startedAtUtc: string;
  expiresAtUtc: string;
}

export interface PlatformIncident {
  incidentCode: string;
  severity: string;
  summary: string;
  detectedAtUtc: string;
}
