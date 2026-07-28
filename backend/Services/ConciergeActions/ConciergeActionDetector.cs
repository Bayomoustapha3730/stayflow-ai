using System.Text.RegularExpressions;
using StayFlow.Api.DTOs.ConciergeActions;
using StayFlow.Api.Models;

namespace StayFlow.Api.Services.ConciergeActions;

public sealed partial class ConciergeActionDetector : IConciergeActionDetector
{
    public ConciergeActionProposal Detect(
        Conversation conversation,
        string guestMessage,
        string? activeTopic,
        bool hasPendingAction)
    {
        var text = Normalize(guestMessage);
        if (string.IsNullOrWhiteSpace(text))
        {
            return None("EmptyMessage");
        }

        if (IsInformationalQuestion(text))
        {
            return None("InformationalQuestion");
        }

        if (ContainsAny(text, "sink", "leak", "broken", "not working", "maintenance"))
        {
            var urgency = ContainsAny(text, "emergency", "fire", "gas", "flood") ? MaintenanceUrgency.Emergency : MaintenanceUrgency.Routine;
            var category = ContainsAny(text, "sink", "leak", "plumb") ? MaintenanceCategory.Plumbing : MaintenanceCategory.Other;
            var action = new MaintenanceTicketAction(conversation.Id, conversation.ReservationId, conversation.PropertyId ?? Guid.Empty, category, Truncate(guestMessage, 260), urgency, null);
            return new ConciergeActionProposal(ConciergeActionType.CreateMaintenanceTicket, ConciergeActionConfidenceLevel.High, action, [], false, null, IsExplicitRequest(text), "MaintenanceKeywords");
        }

        if (ContainsAny(text, "early check", "check in at", "checkin at", "check in") && ContainsAny(text, "can i", "please", "request", "submit", "like to"))
        {
            var time = ParseTime(text);
            var missing = conversation.ReservationId.HasValue ? new List<string>() : ["ReservationId"];
            var action = conversation.ReservationId.HasValue && conversation.PropertyId.HasValue
                ? new EarlyCheckInRequestAction(conversation.Id, conversation.ReservationId.Value, conversation.PropertyId.Value, time, null)
                : null;
            return Proposal(ConciergeActionType.RequestEarlyCheckIn, action, missing, "EarlyCheckIn");
        }

        if (ContainsAny(text, "late checkout", "check out at", "checkout at", "leave at") && ContainsAny(text, "can i", "please", "request", "submit", "like to"))
        {
            var time = ParseTime(text);
            var missing = conversation.ReservationId.HasValue ? new List<string>() : ["ReservationId"];
            var action = conversation.ReservationId.HasValue && conversation.PropertyId.HasValue
                ? new LateCheckoutRequestAction(conversation.Id, conversation.ReservationId.Value, conversation.PropertyId.Value, time, null)
                : null;
            return Proposal(ConciergeActionType.RequestLateCheckout, action, missing, "LateCheckout");
        }

        if (ContainsAny(text, "clean", "housekeeping", "linen", "trash"))
        {
            var missing = conversation.ReservationId.HasValue ? new List<string>() : ["ReservationId"];
            var requestType = ContainsAny(text, "linen") ? HousekeepingRequestType.LinenChange : ContainsAny(text, "trash") ? HousekeepingRequestType.TrashPickup : HousekeepingRequestType.RoomCleaning;
            var date = ParseRelativeDate(text);
            var action = conversation.ReservationId.HasValue && conversation.PropertyId.HasValue
                ? new HousekeepingRequestAction(conversation.Id, conversation.ReservationId.Value, conversation.PropertyId.Value, requestType, date, null)
                : null;
            return Proposal(ConciergeActionType.RequestHousekeeping, action, missing, "Housekeeping");
        }

        if (ContainsAny(text, "towel", "pillows", "pillow", "blanket", "toilet paper", "soap", "water", "toiletries"))
        {
            var quantity = ParseQuantity(text);
            var itemType = ParseExtraItemType(text);
            var missing = new List<string>();
            if (!conversation.ReservationId.HasValue)
            {
                missing.Add("ReservationId");
            }

            if (!quantity.HasValue)
            {
                missing.Add("Quantity");
            }

            var action = conversation.ReservationId.HasValue && conversation.PropertyId.HasValue && quantity.HasValue
                ? new ExtraItemRequestAction(conversation.Id, conversation.ReservationId.Value, conversation.PropertyId.Value, itemType, quantity.Value, null)
                : null;

            return new ConciergeActionProposal(
                ConciergeActionType.RequestExtraItem,
                missing.Count == 0 ? ConciergeActionConfidenceLevel.High : ConciergeActionConfidenceLevel.Medium,
                action,
                missing,
                missing.Count > 0,
                missing.Contains("Quantity") ? "How many items do you need?" : "Could you share a bit more detail for this request?",
                IsExplicitRequest(text),
                "ExtraItem");
        }

        if (ContainsAny(text, "parking", "vehicle", "cars", "car"))
        {
            var count = ParseQuantity(text);
            var missing = new List<string>();
            if (!conversation.ReservationId.HasValue)
            {
                missing.Add("ReservationId");
            }

            if (!count.HasValue)
            {
                missing.Add("VehicleCount");
            }

            var action = conversation.ReservationId.HasValue && conversation.PropertyId.HasValue && count.HasValue
                ? new ParkingRequestAction(conversation.Id, conversation.ReservationId.Value, conversation.PropertyId.Value, count.Value, null, null, null, null)
                : null;

            return new ConciergeActionProposal(
                ConciergeActionType.RequestParking,
                missing.Count == 0 ? ConciergeActionConfidenceLevel.High : ConciergeActionConfidenceLevel.Medium,
                action,
                missing,
                missing.Count > 0,
                missing.Contains("VehicleCount") ? "How many vehicles need parking?" : "Could you share a bit more detail for this request?",
                IsExplicitRequest(text),
                "Parking");
        }

        if (ContainsAny(text, "tell the host", "notify host", "contact host", "host"))
        {
            if (!conversation.PropertyId.HasValue)
            {
                return new ConciergeActionProposal(ConciergeActionType.NotifyHost, ConciergeActionConfidenceLevel.Medium, null, ["PropertyId"], true, "I can notify the host after I verify your reservation context.", IsExplicitRequest(text), "MissingProperty");
            }

            var reason = ContainsAny(text, "late", "arrive") ? HostNotificationReasonCode.GuestArrivalUpdate : HostNotificationReasonCode.GuestNeedsAssistance;
            var action = new HostNotificationAction(conversation.Id, conversation.ReservationId, conversation.PropertyId.Value, reason, HostNotificationPriority.Normal, null);
            return new ConciergeActionProposal(ConciergeActionType.NotifyHost, ConciergeActionConfidenceLevel.High, action, [], false, null, IsExplicitRequest(text), "NotifyHost");
        }

        return None("NoActionDetected");
    }

    private static ConciergeActionProposal Proposal(ConciergeActionType actionType, object? action, IReadOnlyCollection<string> missing, string reason)
    {
        return new ConciergeActionProposal(
            actionType,
            missing.Count == 0 ? ConciergeActionConfidenceLevel.High : ConciergeActionConfidenceLevel.Medium,
            action,
            missing,
            missing.Count > 0,
            missing.Count > 0 ? "Could you share the missing details so I can submit that request?" : null,
            true,
            reason);
    }

    private static ConciergeActionProposal None(string reason)
        => new(ConciergeActionType.None, ConciergeActionConfidenceLevel.None, null, [], false, null, false, reason);

    private static bool IsInformationalQuestion(string text)
    {
        return text.Contains("what time is check", StringComparison.Ordinal)
            || text.Contains("is late checkout available", StringComparison.Ordinal)
            || text.Contains("what time is checkout", StringComparison.Ordinal)
            || text.Contains("is check in", StringComparison.Ordinal)
            || text.Contains("do you have", StringComparison.Ordinal) && !IsExplicitRequest(text);
    }

    private static bool IsExplicitRequest(string text)
    {
        return ContainsAny(text, "can i", "please", "submit", "request", "i need", "i would like", "i'd like", "yes submit");
    }

    private static bool ContainsAny(string text, params string[] phrases)
        => phrases.Any(text.Contains);

    private static string Normalize(string text)
        => string.IsNullOrWhiteSpace(text) ? string.Empty : text.Trim().ToLowerInvariant();

    private static string Truncate(string text, int maxLength)
    {
        var trimmed = text.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private static int? ParseQuantity(string text)
    {
        var match = QuantityRegex().Match(text);
        if (match.Success && int.TryParse(match.Groups[1].Value, out var value))
        {
            return value;
        }

        if (text.Contains("two", StringComparison.Ordinal))
        {
            return 2;
        }

        if (text.Contains("one", StringComparison.Ordinal) || text.Contains("a ", StringComparison.Ordinal))
        {
            return 1;
        }

        return null;
    }

    private static TimeOnly? ParseTime(string text)
    {
        var match = TimeRegex().Match(text);
        if (!match.Success)
        {
            return null;
        }

        if (TimeOnly.TryParse(match.Value, out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static DateOnly? ParseRelativeDate(string text)
    {
        if (text.Contains("tomorrow", StringComparison.Ordinal))
        {
            return DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        }

        return null;
    }

    private static ExtraItemType ParseExtraItemType(string text)
    {
        if (text.Contains("pillow", StringComparison.Ordinal))
        {
            return ExtraItemType.Pillow;
        }

        if (text.Contains("blanket", StringComparison.Ordinal))
        {
            return ExtraItemType.Blanket;
        }

        if (text.Contains("toilet paper", StringComparison.Ordinal))
        {
            return ExtraItemType.ToiletPaper;
        }

        if (text.Contains("soap", StringComparison.Ordinal))
        {
            return ExtraItemType.Soap;
        }

        if (text.Contains("water", StringComparison.Ordinal))
        {
            return ExtraItemType.Water;
        }

        return ExtraItemType.Towel;
    }

    [GeneratedRegex(@"\b(\d{1,2})\b", RegexOptions.CultureInvariant)]
    private static partial Regex QuantityRegex();

    [GeneratedRegex(@"\b\d{1,2}(:\d{2})?\s?(am|pm)?\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TimeRegex();
}
