using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using StayFlow.Api.Configuration;

namespace StayFlow.Api.Services.Email;

public sealed class SmtpEmailSender(IOptions<EmailDeliveryOptions> options) : IEmailSender
{
    private readonly EmailDeliveryOptions emailOptions = options.Value;

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        using var smtpClient = new SmtpClient(emailOptions.Smtp.Host, emailOptions.Smtp.Port)
        {
            EnableSsl = emailOptions.Smtp.EnableSsl,
            Credentials = string.IsNullOrWhiteSpace(emailOptions.Smtp.Username)
                ? CredentialCache.DefaultNetworkCredentials
                : new NetworkCredential(emailOptions.Smtp.Username, emailOptions.Smtp.Password)
        };

        using var mailMessage = new MailMessage(
            new MailAddress(emailOptions.FromAddress, emailOptions.FromName),
            new MailAddress(message.ToAddress))
        {
            Subject = message.Subject,
            Body = string.IsNullOrWhiteSpace(message.HtmlBody) ? message.PlainTextBody : message.HtmlBody,
            IsBodyHtml = !string.IsNullOrWhiteSpace(message.HtmlBody)
        };

        if (!string.IsNullOrWhiteSpace(message.PlainTextBody) && !string.IsNullOrWhiteSpace(message.HtmlBody))
        {
            mailMessage.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(message.PlainTextBody, null, "text/plain"));
        }

        cancellationToken.ThrowIfCancellationRequested();
        await smtpClient.SendMailAsync(mailMessage, cancellationToken);
    }
}