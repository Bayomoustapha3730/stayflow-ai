using StayFlow.Api.Models;

namespace StayFlow.Api.Services;

public interface IResourceCapacityService
{
    Task EnsureCapacityAsync(Guid companyId, UsageMetric metric, CancellationToken cancellationToken);
    Task<long> GetCurrentCountAsync(Guid companyId, UsageMetric metric, CancellationToken cancellationToken);
}
