namespace StayFlow.Api.Services.Email;

public interface IIdentityEmailService
{
    Task SendPasswordResetAsync(string email, string fullName, string token, CancellationToken cancellationToken);
    Task SendEmailVerificationAsync(string email, string fullName, string token, CancellationToken cancellationToken);
    Task SendOrganizationInvitationAsync(string email, string role, string token, CancellationToken cancellationToken);
}