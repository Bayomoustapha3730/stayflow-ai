using System.Globalization;
using StayFlow.Api.Models;

namespace StayFlow.Api.Services;

public sealed class ReservationLifecycleMessageComposer : IReservationLifecycleMessageComposer
{
    // Deterministic Slice 5 language set. An unsupported/missing Guest.PreferredLanguage falls
    // back to English rather than guessing a translation; no AI translation is used.
    private static readonly HashSet<string> SupportedLanguages = new(StringComparer.OrdinalIgnoreCase) { "en", "fr" };

    public GuestJourneyMessageContentSpec Compose(
        ReservationLifecycleEvent lifecycleEvent,
        Reservation reservation,
        Property property,
        Guest guest)
    {
        ArgumentNullException.ThrowIfNull(lifecycleEvent);
        ArgumentNullException.ThrowIfNull(reservation);
        ArgumentNullException.ThrowIfNull(property);
        ArgumentNullException.ThrowIfNull(guest);

        var language = ResolveLanguage(guest.PreferredLanguage);
        var firstName = string.IsNullOrWhiteSpace(guest.FirstName) ? "there" : guest.FirstName.Trim();
        var propertyName = property.Name;

        var content = lifecycleEvent.EventType switch
        {
            ReservationLifecycleEventType.PreArrival => ComposePreArrival(language, firstName, propertyName, reservation.CheckInDate),
            ReservationLifecycleEventType.ArrivalDay => ComposeArrivalDay(language, firstName, propertyName),
            ReservationLifecycleEventType.InStay => ComposeInStay(language, firstName, propertyName),
            ReservationLifecycleEventType.CheckoutDay => ComposeCheckoutDay(language, firstName, propertyName),
            ReservationLifecycleEventType.PostStay => ComposePostStay(language, firstName, propertyName),
            _ => throw new ArgumentOutOfRangeException(nameof(lifecycleEvent), lifecycleEvent.EventType, "Unsupported lifecycle event type.")
        };

        return new GuestJourneyMessageContentSpec(language, content);
    }

    private static string ResolveLanguage(string? preferredLanguage)
    {
        var normalized = preferredLanguage?.Trim();
        return !string.IsNullOrEmpty(normalized) && SupportedLanguages.Contains(normalized)
            ? normalized.ToLowerInvariant()
            : "en";
    }

    private static string ComposePreArrival(string language, string firstName, string propertyName, DateOnly checkInDate)
    {
        var formattedDate = checkInDate.ToString("MMMM d, yyyy", CultureInfo.InvariantCulture);
        return language == "fr"
            ? $"Bonjour {firstName}, ici {propertyName}. Nous avons hâte de vous accueillir à partir du {formattedDate}. Répondez ici si vous avez des questions avant votre arrivée."
            : $"Hi {firstName}, this is {propertyName}. We're looking forward to your stay starting {formattedDate}. Reply here anytime if you have questions before you arrive.";
    }

    private static string ComposeArrivalDay(string language, string firstName, string propertyName)
    {
        return language == "fr"
            ? $"Bonjour {firstName}, aujourd'hui est votre jour d'arrivée à {propertyName}. Répondez ici si vous avez besoin d'aide pour vous installer."
            : $"Hi {firstName}, today is your check-in day at {propertyName}. Reply here if you need any help getting settled in.";
    }

    private static string ComposeInStay(string language, string firstName, string propertyName)
    {
        return language == "fr"
            ? $"Bonjour {firstName}, nous prenons juste des nouvelles pendant votre séjour à {propertyName}. Dites-nous si vous avez besoin de quelque chose."
            : $"Hi {firstName}, just checking in during your stay at {propertyName}. Let us know if there's anything you need.";
    }

    private static string ComposeCheckoutDay(string language, string firstName, string propertyName)
    {
        return language == "fr"
            ? $"Bonjour {firstName}, aujourd'hui est votre jour de départ de {propertyName}. Répondez ici si vous avez besoin d'aide avant de partir."
            : $"Hi {firstName}, today is your checkout day at {propertyName}. Reply here if you need any help before you go.";
    }

    private static string ComposePostStay(string language, string firstName, string propertyName)
    {
        return language == "fr"
            ? $"Merci d'avoir séjourné à {propertyName}, {firstName} ! Nous espérons que vous avez apprécié votre séjour. N'hésitez pas à répondre ici pour partager vos commentaires."
            : $"Thank you for staying at {propertyName}, {firstName}! We hope you enjoyed your stay. Feel free to reply here if you'd like to share any feedback.";
    }
}
