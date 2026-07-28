import type { HttpClient, HttpRequestOptions } from "./httpClient";
import type {
  CreatePropertyKnowledgeRequest,
  PropertyKnowledgeDetail,
  PropertyKnowledgeListQuery,
  PropertyKnowledgePagedResult,
  PropertyKnowledgeSummary,
  UpdatePropertyKnowledgeRequest
} from "../models/propertyKnowledge";

export function createPropertyKnowledgeApi(http: HttpClient) {
  return {
    listKnowledge(propertyId: string, query: PropertyKnowledgeListQuery, options?: HttpRequestOptions) {
      const params = new URLSearchParams();

      if (query.search?.trim()) {
        params.set("search", query.search.trim());
      }

      if (query.category !== undefined) {
        params.set("Category", String(query.category));
      }

      if (query.isApproved !== undefined) {
        params.set("IsApproved", String(query.isApproved));
      }

      if (query.isActive !== undefined) {
        params.set("IsActive", String(query.isActive));
      }

      params.set("PageNumber", String(query.pageNumber));
      params.set("PageSize", String(query.pageSize));

      const queryString = params.toString();
      const path = queryString
        ? `/properties/${propertyId}/knowledge?${queryString}`
        : `/properties/${propertyId}/knowledge`;

      return http.get<PropertyKnowledgePagedResult<PropertyKnowledgeSummary>>(path, options);
    },

    getKnowledgeItem(propertyId: string, knowledgeId: string, options?: HttpRequestOptions) {
      return http.get<PropertyKnowledgeDetail>(`/properties/${propertyId}/knowledge/${knowledgeId}`, options);
    },

    createKnowledge(propertyId: string, request: CreatePropertyKnowledgeRequest, options?: HttpRequestOptions) {
      return http.post<PropertyKnowledgeDetail>(`/properties/${propertyId}/knowledge`, request, options);
    },

    updateKnowledge(propertyId: string, knowledgeId: string, request: UpdatePropertyKnowledgeRequest, options?: HttpRequestOptions) {
      return http.put<PropertyKnowledgeDetail>(`/properties/${propertyId}/knowledge/${knowledgeId}`, request, options);
    },

    approveKnowledge(propertyId: string, knowledgeId: string, options?: HttpRequestOptions) {
      return http.post<PropertyKnowledgeDetail>(`/properties/${propertyId}/knowledge/${knowledgeId}/approve`, undefined, options);
    },

    unapproveKnowledge(propertyId: string, knowledgeId: string, options?: HttpRequestOptions) {
      return http.post<PropertyKnowledgeDetail>(`/properties/${propertyId}/knowledge/${knowledgeId}/unapprove`, undefined, options);
    },

    activateKnowledge(propertyId: string, knowledgeId: string, options?: HttpRequestOptions) {
      return http.post<PropertyKnowledgeDetail>(`/properties/${propertyId}/knowledge/${knowledgeId}/activate`, undefined, options);
    },

    deactivateKnowledge(propertyId: string, knowledgeId: string, options?: HttpRequestOptions) {
      return http.post<PropertyKnowledgeDetail>(`/properties/${propertyId}/knowledge/${knowledgeId}/deactivate`, undefined, options);
    },

    deleteKnowledge(propertyId: string, knowledgeId: string, options?: HttpRequestOptions) {
      return http.delete<{ id: string }>(`/properties/${propertyId}/knowledge/${knowledgeId}`, options);
    }
  };
}
