using Microsoft.Extensions.Options;
using StayFlow.Api.Configuration;

namespace StayFlow.Api.Services.Email;

public sealed class IdentityEmailService(
    IEmailSender emailSender,
    IOptions<EmailDeliveryOptions> options) : IIdentityEmailService
{
    private readonly EmailDeliveryOptions emailOptions = options.Value;

    public Task SendPasswordResetAsync(string email, string fullName, string token, CancellationToken cancellationToken)
    {
        var link = BuildFrontendLink("/auth/reset-password", token);
        return emailSender.SendAsync(new EmailMessage
        {
            ToAddress = email,
            Subject = "Reset your StayFlow password",
            PlainTextBody = $"Hello {ResolveRecipientName(fullName)},\n\nUse this link to reset your password: {link}\n\nIf you did not request this, you can ignore this email.",
            HtmlBody = $"<p>Hello {System.Net.WebUtility.HtmlEncode(ResolveRecipientName(fullName))},</p><p>Use this link to reset your password:</p><p><a href=\"{System.Net.WebUtility.HtmlEncode(link)}\">Reset password</a></p><p>If you did not request this, you can ignore this email.</p>"
        }, cancellationToken);
    }

    public Task SendEmailVerificationAsync(string email, string fullName, string token, CancellationToken cancellationToken)
    {
        var link = BuildFrontendLink("/auth/verify-email", token);
        return emailSender.SendAsync(new EmailMessage
        {
            ToAddress = email,
            Subject = "Verify your StayFlow email",
            PlainTextBody = $"Hello {ResolveRecipientName(fullName)},\n\nUse this link to verify your email: {link}",
            HtmlBody = $"<p>Hello {System.Net.WebUtility.HtmlEncode(ResolveRecipientName(fullName))},</p><p>Use this link to verify your email:</p><p><a href=\"{System.Net.WebUtility.HtmlEncode(link)}\">Verify email</a></p>"
        }, cancellationToken);
    }

    public Task SendOrganizationInvitationAsync(string email, string role, string token, CancellationToken cancellationToken)
    {
        var link = BuildFrontendLink("/invitation/respond", token);
        return emailSender.SendAsync(new EmailMessage
        {
            ToAddress = email,
            Subject = "You have been invited to StayFlow",
            PlainTextBody = $"You have been invited to join StayFlow as {role}. Use this link to review the invitation: {link}",
            HtmlBody = $"<p>You have been invited to join StayFlow as {System.Net.WebUtility.HtmlEncode(role)}.</p><p><a href=\"{System.Net.WebUtility.HtmlEncode(link)}\">Review invitation</a></p>"
        }, cancellationToken);
    }

    private string BuildFrontendLink(string path, string token)
    {
        var baseUrl = emailOptions.FrontendBaseUrl.TrimEnd('/');
        return $"{baseUrl}{path}?token={Uri.EscapeDataString(token)}";
    }

    private static string ResolveRecipientName(string fullName)
    {
        return string.IsNullOrWhiteSpace(fullName) ? "there" : fullName.Trim();
    }
}