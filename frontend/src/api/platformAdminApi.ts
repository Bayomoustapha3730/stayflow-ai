import type {
  PlatformBackgroundJobStatus,
  PlatformBillingHealth,
  PlatformEmailDeliveryMonitoring,
  PlatformFeatureFlag,
  PlatformIncident,
  PlatformOperationalMetrics,
  PlatformOrganizationHealth,
  PlatformProviderHealth,
  PlatformQueueMonitoring,
  PlatformReadOnlyDiagnostic,
  PlatformSupportImpersonationSession,
  PlatformSystemConfiguration,
  PlatformTenantDetail,
  PlatformTenantLifecycleAudit,
  PlatformTenantPagedResult,
  PlatformUsageOverview,
  PlatformWebhookMonitoring
} from "../models/platformAdmin";
import type { HttpClient } from "./httpClient";

export interface PlatformTenantQuery {
  search?: string;
  status?: string;
  page?: number;
  pageSize?: number;
}

export interface PlatformUpdateFeatureFlagRequest {
  isEnabled: boolean;
  quotaLimit?: number | null;
  isUnlimited?: boolean;
  unit?: string | null;
  notes?: string | null;
}

export interface PlatformTenantActionRequest {
  reason: string;
}

export interface PlatformTenantRepairRequest {
  normalizeStatusAndActivation?: boolean;
  recomputeSubscriptionSnapshot?: boolean;
  reason: string;
}

export interface PlatformSubscriptionSyncRequest {
  reason: string;
}

export interface PlatformSupportImpersonationStartRequest {
  targetCompanyId: string;
  targetUserId: string;
  reason: string;
  explicitAuthorizationCode: string;
}

export interface PlatformSupportImpersonationEndRequest {
  reason: string;
}

function toQueryString(query: PlatformTenantQuery): string {
  const params = new URLSearchParams();
  if (query.search?.trim()) {
    params.set("search", query.search.trim());
  }
  if (query.status?.trim()) {
    params.set("status", query.status.trim());
  }
  if (query.page) {
    params.set("page", String(query.page));
  }
  if (query.pageSize) {
    params.set("pageSize", String(query.pageSize));
  }

  const value = params.toString();
  return value ? `?${value}` : "";
}

export function createPlatformAdminApi(http: HttpClient) {
  return {
    listTenants: async (query: PlatformTenantQuery = {}): Promise<PlatformTenantPagedResult> =>
      http.get<PlatformTenantPagedResult>(`/api/platform-admin/tenants${toQueryString(query)}`),

    getTenant: async (companyId: string): Promise<PlatformTenantDetail> =>
      http.get<PlatformTenantDetail>(`/api/platform-admin/tenants/${companyId}`),

    suspendTenant: async (companyId: string, request: PlatformTenantActionRequest): Promise<PlatformTenantDetail> =>
      http.post<PlatformTenantDetail>(`/api/platform-admin/tenants/${companyId}/suspend`, request),

    reactivateTenant: async (companyId: string, request: PlatformTenantActionRequest): Promise<PlatformTenantDetail> =>
      http.post<PlatformTenantDetail>(`/api/platform-admin/tenants/${companyId}/reactivate`, request),

    archiveTenant: async (companyId: string, request: PlatformTenantActionRequest): Promise<PlatformTenantDetail> =>
      http.post<PlatformTenantDetail>(`/api/platform-admin/tenants/${companyId}/archive`, request),

    restoreTenant: async (companyId: string, request: PlatformTenantActionRequest): Promise<PlatformTenantDetail> =>
      http.post<PlatformTenantDetail>(`/api/platform-admin/tenants/${companyId}/restore`, request),

    getTenantAudit: async (companyId: string): Promise<PlatformTenantLifecycleAudit[]> =>
      http.get<PlatformTenantLifecycleAudit[]>(`/api/platform-admin/tenants/${companyId}/audit`),

    getTenantHealth: async (companyId: string): Promise<PlatformOrganizationHealth> =>
      http.get<PlatformOrganizationHealth>(`/api/platform-admin/tenants/${companyId}/health`),

    getUsageOverview: async (): Promise<PlatformUsageOverview> =>
      http.get<PlatformUsageOverview>("/api/platform-admin/usage"),

    getOperationalMetrics: async (): Promise<PlatformOperationalMetrics> =>
      http.get<PlatformOperationalMetrics>("/api/platform-admin/operations/metrics"),

    getBackgroundJobs: async (): Promise<PlatformBackgroundJobStatus[]> =>
      http.get<PlatformBackgroundJobStatus[]>("/api/platform-admin/operations/background-jobs"),

    getWebhooks: async (): Promise<PlatformWebhookMonitoring[]> =>
      http.get<PlatformWebhookMonitoring[]>("/api/platform-admin/operations/webhooks"),

    getQueues: async (): Promise<PlatformQueueMonitoring[]> =>
      http.get<PlatformQueueMonitoring[]>("/api/platform-admin/operations/queues"),

    getEmailDelivery: async (): Promise<PlatformEmailDeliveryMonitoring> =>
      http.get<PlatformEmailDeliveryMonitoring>("/api/platform-admin/operations/email-delivery"),

    getProviderHealth: async (): Promise<PlatformProviderHealth[]> =>
      http.get<PlatformProviderHealth[]>("/api/platform-admin/providers/health"),

    getBillingHealth: async (): Promise<PlatformBillingHealth> =>
      http.get<PlatformBillingHealth>("/api/platform-admin/billing/health"),

    listFeatureFlags: async (): Promise<PlatformFeatureFlag[]> =>
      http.get<PlatformFeatureFlag[]>("/api/platform-admin/feature-flags"),

    updateFeatureFlag: async (
      planId: string,
      flagKey: string,
      request: PlatformUpdateFeatureFlagRequest
    ): Promise<PlatformFeatureFlag> =>
      http.put<PlatformFeatureFlag>(`/api/platform-admin/feature-flags/${planId}/${encodeURIComponent(flagKey)}`, request),

    synchronizeSubscription: async (companyId: string, request: PlatformSubscriptionSyncRequest): Promise<Record<string, unknown>> =>
      http.post<Record<string, unknown>>(`/api/platform-admin/subscriptions/${companyId}/synchronize`, request),

    repairTenant: async (companyId: string, request: PlatformTenantRepairRequest): Promise<Record<string, unknown>> =>
      http.post<Record<string, unknown>>(`/api/platform-admin/tenants/${companyId}/repair`, request),

    getReadOnlyDiagnostics: async (): Promise<PlatformReadOnlyDiagnostic[]> =>
      http.get<PlatformReadOnlyDiagnostic[]>("/api/platform-admin/diagnostics/read-only"),

    getSystemConfiguration: async (): Promise<PlatformSystemConfiguration> =>
      http.get<PlatformSystemConfiguration>("/api/platform-admin/system-configuration"),

    startSupportImpersonation: async (request: PlatformSupportImpersonationStartRequest): Promise<PlatformSupportImpersonationSession> =>
      http.post<PlatformSupportImpersonationSession>("/api/platform-admin/support/impersonation/start", request),

    endSupportImpersonation: async (sessionId: string, request: PlatformSupportImpersonationEndRequest): Promise<Record<string, unknown>> =>
      http.post<Record<string, unknown>>(`/api/platform-admin/support/impersonation/${sessionId}/end`, request),

    getIncidents: async (): Promise<PlatformIncident[]> =>
      http.get<PlatformIncident[]>("/api/platform-admin/incidents")
  };
}
