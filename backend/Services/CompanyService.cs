using System.Text.Json;
using StayFlow.Api.Common;
using StayFlow.Api.DTOs.Companies;
using StayFlow.Api.Models;
using StayFlow.Api.Repositories;

namespace StayFlow.Api.Services;

public sealed class CompanyService(ICompanyRepository companyRepository) : ICompanyService
{
    public async Task<ApiResponse<PagedResult<CompanyDto>>> GetAsync(
        CompanyQueryParameters query,
        CancellationToken cancellationToken)
    {
        var companies = await companyRepository.GetAsync(query, cancellationToken);

        return ApiResponse<PagedResult<CompanyDto>>.Ok(new PagedResult<CompanyDto>
        {
            Items = companies.Items.Select(MapToDto).ToList(),
            PageNumber = companies.PageNumber,
            PageSize = companies.PageSize,
            TotalCount = companies.TotalCount
        });
    }

    public async Task<ApiResponse<CompanyDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var company = await companyRepository.GetByIdAsync(id, cancellationToken);
        return company is null
            ? ApiResponse<CompanyDto>.Fail("Company was not found.")
            : ApiResponse<CompanyDto>.Ok(MapToDto(company));
    }

    public async Task<ApiResponse<CompanyDto>> CreateAsync(
        CreateCompanyRequest request,
        CancellationToken cancellationToken)
    {
        var validation = CompanyRequestValidator.Validate(request);
        if (!validation.IsValid)
        {
            return ApiResponse<CompanyDto>.Fail("Company validation failed.", validation.Errors);
        }

        var company = new Company
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            LegalName = NormalizeOptional(request.LegalName),
            Email = request.Email.Trim(),
            PhoneNumber = request.PhoneNumber.Trim(),
            CountryCode = request.CountryCode.Trim().ToUpperInvariant(),
            TimeZone = request.TimeZone.Trim(),
            IsActive = true
        };

        await companyRepository.AddAsync(company, cancellationToken);
        await AddAuditLogAsync("Created", company, cancellationToken);
        await companyRepository.SaveChangesAsync(cancellationToken);

        return ApiResponse<CompanyDto>.Ok(MapToDto(company), "Company created successfully.");
    }

    public async Task<ApiResponse<CompanyDto>> UpdateAsync(
        Guid id,
        UpdateCompanyRequest request,
        CancellationToken cancellationToken)
    {
        var validation = CompanyRequestValidator.Validate(request);
        if (!validation.IsValid)
        {
            return ApiResponse<CompanyDto>.Fail("Company validation failed.", validation.Errors);
        }

        var company = await companyRepository.GetByIdAsync(id, cancellationToken);
        if (company is null)
        {
            return ApiResponse<CompanyDto>.Fail("Company was not found.");
        }

        company.Name = request.Name.Trim();
        company.LegalName = NormalizeOptional(request.LegalName);
        company.Email = request.Email.Trim();
        company.PhoneNumber = request.PhoneNumber.Trim();
        company.CountryCode = request.CountryCode.Trim().ToUpperInvariant();
        company.TimeZone = request.TimeZone.Trim();
        company.IsActive = request.IsActive;

        await AddAuditLogAsync("Updated", company, cancellationToken);
        await companyRepository.SaveChangesAsync(cancellationToken);

        return ApiResponse<CompanyDto>.Ok(MapToDto(company), "Company updated successfully.");
    }

    public async Task<ApiResponse<object>> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var company = await companyRepository.GetByIdAsync(id, cancellationToken);
        if (company is null)
        {
            return ApiResponse<object>.Fail("Company was not found.");
        }

        company.IsActive = false;

        await AddAuditLogAsync("Deleted", company, cancellationToken);
        await companyRepository.SaveChangesAsync(cancellationToken);

        return ApiResponse<object>.Ok(new { company.Id }, "Company deleted successfully.");
    }

    private async Task AddAuditLogAsync(string action, Company company, CancellationToken cancellationToken)
    {
        var auditLog = new AuditLog
        {
            Id = Guid.NewGuid(),
            EntityName = nameof(Company),
            EntityId = company.Id,
            Action = action,
            Details = JsonSerializer.Serialize(new
            {
                company.Name,
                company.Email,
                company.PhoneNumber,
                company.IsActive
            }),
            CreatedAt = DateTimeOffset.UtcNow
        };

        await companyRepository.AddAuditLogAsync(auditLog, cancellationToken);
    }

    private static CompanyDto MapToDto(Company company)
    {
        return new CompanyDto
        {
            Id = company.Id,
            Name = company.Name,
            LegalName = company.LegalName,
            Email = company.Email,
            PhoneNumber = company.PhoneNumber,
            CountryCode = company.CountryCode,
            TimeZone = company.TimeZone,
            IsActive = company.IsActive,
            CreatedAt = company.CreatedAt,
            UpdatedAt = company.UpdatedAt
        };
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
