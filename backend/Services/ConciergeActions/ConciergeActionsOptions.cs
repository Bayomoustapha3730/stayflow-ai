namespace StayFlow.Api.Services.ConciergeActions;

public sealed class ConciergeActionsOptions
{
    public const string SectionName = "ConciergeActions";

    public bool Enabled { get; init; } = true;
    public int PendingActionExpirationMinutes { get; init; } = 30;
    public int MaximumActionsPerConversationPerHour { get; init; } = 5;
    public int MaximumExtraItemQuantity { get; init; } = 10;
    public int MaximumVehicleCount { get; init; } = 5;
    public int MaximumNoteLength { get; init; } = 240;
    public bool EnableMaintenance { get; init; } = true;
    public bool EnableHousekeeping { get; init; } = true;
    public bool EnableParking { get; init; } = true;
    public bool EnableEarlyCheckIn { get; init; } = true;
    public bool EnableLateCheckout { get; init; } = true;
    public bool EnableHostNotification { get; init; } = true;
}
