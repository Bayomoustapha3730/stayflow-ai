namespace StayFlow.Api.Services.Email;

public sealed class DevelopmentEmailInbox
{
    private readonly List<EmailMessage> messages = [];
    private readonly object sync = new();

    public IReadOnlyCollection<EmailMessage> Messages
    {
        get
        {
            lock (sync)
            {
                return messages.ToList();
            }
        }
    }

    public void Add(EmailMessage message)
    {
        lock (sync)
        {
            messages.Add(message);
        }
    }
}