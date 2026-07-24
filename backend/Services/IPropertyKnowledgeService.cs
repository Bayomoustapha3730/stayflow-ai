using StayFlow.Api.Common;
using StayFlow.Api.DTOs.PropertyKnowledge;

namespace StayFlow.Api.Services;

public interface IPropertyKnowledgeService
{
    Task<ApiResponse<PagedResult<PropertyKnowledgeSummaryResponse>>> GetAsync(Guid propertyId, PropertyKnowledgeListQuery query, CancellationToken cancellationToken);
    Task<ApiResponse<PropertyKnowledgeDetailResponse>> GetByIdAsync(Guid propertyId, Guid knowledgeId, CancellationToken cancellationToken);
    Task<ApiResponse<PropertyKnowledgeDetailResponse>> CreateAsync(Guid propertyId, CreatePropertyKnowledgeRequest request, CancellationToken cancellationToken);
    Task<ApiResponse<PropertyKnowledgeDetailResponse>> UpdateAsync(Guid propertyId, Guid knowledgeId, UpdatePropertyKnowledgeRequest request, CancellationToken cancellationToken);
    Task<ApiResponse<PropertyKnowledgeDetailResponse>> ApproveAsync(Guid propertyId, Guid knowledgeId, CancellationToken cancellationToken);
    Task<ApiResponse<PropertyKnowledgeDetailResponse>> UnapproveAsync(Guid propertyId, Guid knowledgeId, CancellationToken cancellationToken);
    Task<ApiResponse<PropertyKnowledgeDetailResponse>> ActivateAsync(Guid propertyId, Guid knowledgeId, CancellationToken cancellationToken);
    Task<ApiResponse<PropertyKnowledgeDetailResponse>> DeactivateAsync(Guid propertyId, Guid knowledgeId, CancellationToken cancellationToken);
    Task<ApiResponse<object>> DeleteAsync(Guid propertyId, Guid knowledgeId, CancellationToken cancellationToken);
}