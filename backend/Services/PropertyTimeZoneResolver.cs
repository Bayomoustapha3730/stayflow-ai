namespace StayFlow.Api.Services;

// Shared timezone resolution helper. Mirrors the validation behavior established in
// ReservationLifecycleService without modifying that class's lifecycle rules.
public static class PropertyTimeZoneResolver
{
    public static TimeZoneInfo Resolve(string? propertyTimeZone)
    {
        if (string.IsNullOrWhiteSpace(propertyTimeZone))
        {
            throw new ArgumentException("Property timezone is required.", nameof(propertyTimeZone));
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(propertyTimeZone.Trim());
        }
        catch (TimeZoneNotFoundException exception)
        {
            throw new ArgumentException($"Property timezone '{propertyTimeZone}' was not found.", nameof(propertyTimeZone), exception);
        }
        catch (InvalidTimeZoneException exception)
        {
            throw new ArgumentException($"Property timezone '{propertyTimeZone}' is invalid.", nameof(propertyTimeZone), exception);
        }
    }
}
