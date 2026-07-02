using StayFlow.Api.Common;
using StayFlow.Api.DTOs.Properties;

namespace StayFlow.Api.Services;

public interface IPropertyService
{
    Task<ApiResponse<PagedResult<PropertySummaryDto>>> GetAsync(PropertyQueryParameters query, CancellationToken cancellationToken);
    Task<ApiResponse<PropertyDto>> GetByIdAsync(Guid id, Guid companyId, CancellationToken cancellationToken);
    Task<ApiResponse<PropertyDto>> CreateAsync(CreatePropertyRequest request, CancellationToken cancellationToken);
    Task<ApiResponse<PropertyDto>> UpdateAsync(Guid id, UpdatePropertyRequest request, CancellationToken cancellationToken);
    Task<ApiResponse<object>> DeleteAsync(Guid id, Guid companyId, CancellationToken cancellationToken);
}
