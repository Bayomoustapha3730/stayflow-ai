namespace StayFlow.Api.Models;

public sealed class GuestJourneyMessage : AuditableEntity
{
    public Guid CompanyId { get; set; }
    public Guid ReservationId { get; set; }
    public Guid ReservationLifecycleEventId { get; set; }
    public Guid PropertyId { get; set; }
    public Guid GuestId { get; set; }
    public Guid? ConversationId { get; set; }
    public Guid? ConversationMessageId { get; set; }
    public ReservationLifecycleEventType JourneyEventType { get; set; }
    public GuestJourneyMessageChannel Channel { get; set; } = GuestJourneyMessageChannel.WhatsApp;
    public string Language { get; set; } = "en";
    public GuestJourneyMessageContentType ContentType { get; set; } = GuestJourneyMessageContentType.Text;
    public string RenderedContent { get; set; } = string.Empty;
    public string? TemplateName { get; set; }
    public string? TemplateParametersJson { get; set; }
    public GuestJourneyMessageStatus Status { get; set; } = GuestJourneyMessageStatus.Pending;
    public int AttemptCount { get; set; }
    public DateTimeOffset? LastAttemptAtUtc { get; set; }
    public DateTimeOffset? NextAttemptAtUtc { get; set; }
    public string? ProviderMessageId { get; set; }
    public DateTimeOffset? AcceptedAtUtc { get; set; }
    public DateTimeOffset? DeliveredAtUtc { get; set; }
    public DateTimeOffset? FailedAtUtc { get; set; }
    public string? LastError { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;

    public Company Company { get; set; } = null!;
    public Reservation Reservation { get; set; } = null!;
    public ReservationLifecycleEvent ReservationLifecycleEvent { get; set; } = null!;
    public Property Property { get; set; } = null!;
    public Guest Guest { get; set; } = null!;
    public Conversation? Conversation { get; set; }
    public ConversationMessage? ConversationMessage { get; set; }
}