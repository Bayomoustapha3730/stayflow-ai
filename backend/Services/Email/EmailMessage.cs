namespace StayFlow.Api.Services.Email;

public sealed class EmailMessage
{
    public string ToAddress { get; init; } = string.Empty;
    public string Subject { get; init; } = string.Empty;
    public string PlainTextBody { get; init; } = string.Empty;
    public string HtmlBody { get; init; } = string.Empty;
}