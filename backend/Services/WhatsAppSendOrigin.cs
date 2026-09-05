namespace StayFlow.Api.Services;

/// <summary>
/// Identifies which subsystem initiated an outbound WhatsApp send so the outbound gate can apply
/// origin-specific production policy. Values deliberately start at 1 so that <c>default</c> (0) is
/// an undefined origin the gate always denies, preventing an unset origin from being treated as a
/// manual host reply.
/// </summary>
public enum WhatsAppSendOrigin
{
    ManualHost = 1,
    AiConcierge = 2,
    GuestJourney = 3,
    ReservationLifecycle = 4,
    Retry = 5,
    TemplateManual = 6,
    SystemOther = 7
}
