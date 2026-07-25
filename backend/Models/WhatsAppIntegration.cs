namespace StayFlow.Api.Models;

public sealed class WhatsAppIntegration : AuditableEntity
{
    public Guid CompanyId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string PhoneNumberId { get; set; } = string.Empty;
    public string WhatsAppBusinessAccountId { get; set; } = string.Empty;
    public string BusinessPhoneNumberMasked { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public Company Company { get; set; } = null!;
}