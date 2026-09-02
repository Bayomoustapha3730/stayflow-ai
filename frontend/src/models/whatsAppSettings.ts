export interface WhatsAppIntegrationSummary {
  id: string;
  displayName: string;
  businessPhoneNumberMasked: string;
  isActive: boolean;
  isProductionEnabled: boolean;
  mode: string;
  healthStatus: string;
  lastHealthCheckAt?: string | null;
  lastSuccessfulHealthCheckAt?: string | null;
  lastTemplateSyncAt?: string | null;
  lastErrorSummary?: string | null;
}

export interface WhatsAppIntegrationConfiguration {
  displayName: string;
  phoneNumberId: string;
  whatsAppBusinessAccountId: string;
  businessPhoneNumberMasked: string;
  credentialReference?: string | null;
  graphApiVersion: string;
  isActive: boolean;
}

export interface WhatsAppIntegrationDetail extends WhatsAppIntegrationConfiguration {
  id: string;
  isProductionEnabled: boolean;
  mode: string;
  healthStatus: string;
  lastHealthCheckAt?: string | null;
  lastSuccessfulHealthCheckAt?: string | null;
  lastTemplateSyncAt?: string | null;
  lastErrorSummary?: string | null;
}

export interface WhatsAppProductionEnableResult {
  integrationId: string;
  isProductionEnabled: boolean;
  status: string;
  message: string;
  checkedAt: string;
}

export interface WhatsAppIntegrationHealth {
  integrationId: string;
  status: string;
  message: string;
  isSendCapable: boolean;
  checkedAt: string;
}

export interface WhatsAppTemplateVariableDefinition {
  position: number;
  placeholder: string;
}

export interface WhatsAppTemplateSummary {
  id: string;
  name: string;
  languageCode: string;
  category: string;
  status: string;
  isActive: boolean;
  isApproved: boolean;
  variableCount: number;
  lastSyncedAt?: string | null;
}

export interface WhatsAppTemplateDetail extends WhatsAppTemplateSummary {
  headerType?: string | null;
  bodyText: string;
  footerText?: string | null;
  variables: WhatsAppTemplateVariableDefinition[];
}

export interface WhatsAppTemplateListQuery {
  status?: string;
  language?: string;
  category?: string;
  search?: string;
  active?: boolean;
  approvedOnly?: boolean;
  page?: number;
  pageSize?: number;
}

export interface WhatsAppTemplateListResponse {
  items: WhatsAppTemplateSummary[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface WhatsAppTemplateSyncResult {
  added: number;
  updated: number;
  unchanged: number;
  disabled: number;
  failed: number;
  syncedAt: string;
  status: string;
  message?: string | null;
}

export interface WhatsAppTemplatePreviewRequest {
  variables: string[];
}

export interface WhatsAppTemplatePreview {
  headerPreview: string;
  bodyPreview: string;
  footerPreview: string;
  hasMissingVariables: boolean;
}
