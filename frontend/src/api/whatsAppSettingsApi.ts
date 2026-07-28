import type { HttpClient } from "./httpClient";
import type {
  WhatsAppIntegrationHealth,
  WhatsAppIntegrationSummary,
  WhatsAppTemplateDetail,
  WhatsAppTemplateListQuery,
  WhatsAppTemplateListResponse,
  WhatsAppTemplatePreview,
  WhatsAppTemplatePreviewRequest,
  WhatsAppTemplateSyncResult
} from "../models/whatsAppSettings";

export function createWhatsAppSettingsApi(http: HttpClient) {
  return {
    listIntegrations() {
      return http.get<WhatsAppIntegrationSummary[]>("/whatsapp/integrations");
    },

    checkIntegrationHealth(integrationId: string) {
      return http.get<WhatsAppIntegrationHealth>(`/whatsapp/integrations/${integrationId}/health`);
    },

    syncTemplates(integrationId: string) {
      return http.post<WhatsAppTemplateSyncResult>(`/whatsapp/integrations/${integrationId}/templates/sync`);
    },

    listTemplates(integrationId: string, query: WhatsAppTemplateListQuery) {
      const params = new URLSearchParams();

      if (query.status?.trim()) {
        params.set("status", query.status.trim());
      }

      if (query.language?.trim()) {
        params.set("language", query.language.trim());
      }

      if (query.category?.trim()) {
        params.set("category", query.category.trim());
      }

      if (query.search?.trim()) {
        params.set("search", query.search.trim());
      }

      if (query.active !== undefined) {
        params.set("active", String(query.active));
      }

      if (query.approvedOnly !== undefined) {
        params.set("approvedOnly", String(query.approvedOnly));
      }

      if (query.page !== undefined) {
        params.set("page", String(query.page));
      }

      if (query.pageSize !== undefined) {
        params.set("pageSize", String(query.pageSize));
      }

      const queryString = params.toString();
      const path = queryString
        ? `/whatsapp/integrations/${integrationId}/templates?${queryString}`
        : `/whatsapp/integrations/${integrationId}/templates`;

      return http.get<WhatsAppTemplateListResponse>(path);
    },

    getTemplate(integrationId: string, templateId: string) {
      return http.get<WhatsAppTemplateDetail>(`/whatsapp/integrations/${integrationId}/templates/${templateId}`);
    },

    previewTemplate(integrationId: string, templateId: string, request: WhatsAppTemplatePreviewRequest) {
      return http.post<WhatsAppTemplatePreview>(`/whatsapp/integrations/${integrationId}/templates/${templateId}/preview`, request);
    }
  };
}
