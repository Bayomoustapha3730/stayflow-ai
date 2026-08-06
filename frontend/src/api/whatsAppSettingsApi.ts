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
  interface RequestOptions {
    signal?: AbortSignal;
  }

  return {
    listIntegrations(options?: RequestOptions) {
      return http.get<WhatsAppIntegrationSummary[]>("/whatsapp/integrations", options);
    },

    checkIntegrationHealth(integrationId: string, options?: RequestOptions) {
      return http.get<WhatsAppIntegrationHealth>(`/whatsapp/integrations/${integrationId}/health`, options);
    },

    syncTemplates(integrationId: string, options?: RequestOptions) {
      return http.post<WhatsAppTemplateSyncResult>(`/whatsapp/integrations/${integrationId}/templates/sync`, undefined, options);
    },

    listTemplates(integrationId: string, query: WhatsAppTemplateListQuery, options?: RequestOptions) {
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

      return http.get<WhatsAppTemplateListResponse>(path, options);
    },

    getTemplate(integrationId: string, templateId: string, options?: RequestOptions) {
      return http.get<WhatsAppTemplateDetail>(`/whatsapp/integrations/${integrationId}/templates/${templateId}`, options);
    },

    previewTemplate(integrationId: string, templateId: string, request: WhatsAppTemplatePreviewRequest, options?: RequestOptions) {
      return http.post<WhatsAppTemplatePreview>(`/whatsapp/integrations/${integrationId}/templates/${templateId}/preview`, request, options);
    }
  };
}
