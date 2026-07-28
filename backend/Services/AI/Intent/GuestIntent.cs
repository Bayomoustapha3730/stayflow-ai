namespace StayFlow.Api.Services.AI.Intent;

public enum GuestIntent
{
    WiFi = 0,
    CheckIn = 1,
    Checkout = 2,
    Parking = 3,
    HouseRules = 4,
    Emergency = 5,
    LocalRecommendations = 6,
    Amenities = 7,
    Access = 8,
    GeneralProperty = 9,
    Unknown = 10,

    // Concierge v2 intents.
    PetPolicy = 23,
    PropertyAccess = 24,
    Reservation = 25,
    Payment = 26,
    HostContact = 27,

    // Legacy intents remain supported for backward compatibility in existing flows/tests.
    Laundry = 11,
    Thermostat = 12,
    Trash = 13,
    Accessibility = 14,
    Maintenance = 15,
    Noise = 16,
    Refund = 17,
    Cancellation = 18,
    ReservationChange = 19,
    LateArrival = 20,
    EarlyCheckIn = 21,
    GeneralQuestion = 22
}
