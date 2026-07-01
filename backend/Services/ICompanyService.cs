using StayFlow.Api.Common;
using StayFlow.Api.DTOs.Companies;

namespace StayFlow.Api.Services;

public interface ICompanyService
{
    Task<ApiResponse<PagedResult<CompanyDto>>> GetAsync(CompanyQueryParameters query, CancellationToken cancellationToken);
    Task<ApiResponse<CompanyDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<ApiResponse<CompanyDto>> CreateAsync(CreateCompanyRequest request, CancellationToken cancellationToken);
    Task<ApiResponse<CompanyDto>> UpdateAsync(Guid id, UpdateCompanyRequest request, CancellationToken cancellationToken);
    Task<ApiResponse<object>> DeleteAsync(Guid id, CancellationToken cancellationToken);
}
