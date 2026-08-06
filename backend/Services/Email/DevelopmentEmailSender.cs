namespace StayFlow.Api.Services.Email;

public sealed class DevelopmentEmailSender(DevelopmentEmailInbox inbox) : IEmailSender
{
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        inbox.Add(message);
        return Task.CompletedTask;
    }
}