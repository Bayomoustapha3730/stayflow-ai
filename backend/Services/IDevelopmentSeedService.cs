namespace StayFlow.Api.Services;

/// <summary>
/// Development-only seeder for AI testing data.
/// Provides demo user, guest, and reservation for end-to-end testing.
/// </summary>
public interface IDevelopmentSeedService
{
    /// <summary>
    /// Seeds development test data if a password is configured.
    /// This service is only available in Development environment.
    /// </summary>
    Task SeedAsync(CancellationToken cancellationToken);
}
