namespace StayFlow.Api.Configuration;

public sealed class EmailDeliveryOptions
{
    public const string SectionName = "Email";

    public string Provider { get; init; } = "Development";
    public string FromAddress { get; init; } = "no-reply@example.invalid";
    public string FromName { get; init; } = "StayFlow";
    public string FrontendBaseUrl { get; init; } = "http://localhost:5173";
    public SmtpEmailOptions Smtp { get; init; } = new();
    public SendGridCompatibleEmailOptions SendGrid { get; init; } = new();
    public AzureCommunicationServicesEmailOptions AzureCommunicationServices { get; init; } = new();
}

public sealed class SmtpEmailOptions
{
    public string Host { get; init; } = string.Empty;
    public int Port { get; init; } = 587;
    public string Username { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public bool EnableSsl { get; init; } = true;
}

public sealed class SendGridCompatibleEmailOptions
{
    public string BaseUrl { get; init; } = "https://api.sendgrid.com";
    public string ApiKey { get; init; } = string.Empty;
    public string SendPath { get; init; } = "/v3/mail/send";
}

public sealed class AzureCommunicationServicesEmailOptions
{
    public string Endpoint { get; init; } = string.Empty;
    public string ApiKey { get; init; } = string.Empty;
    public string ApiVersion { get; init; } = "2023-03-31";
}