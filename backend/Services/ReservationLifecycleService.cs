using Microsoft.Extensions.Options;
using StayFlow.Api.Models;

namespace StayFlow.Api.Services;

public sealed class ReservationLifecycleService(
    TimeProvider timeProvider,
    IOptions<ReservationContextOptions> options) : IReservationLifecycleService
{
    public ReservationLifecycleContext GetContext(Reservation reservation, Property property)
    {
        ArgumentNullException.ThrowIfNull(reservation);
        ArgumentNullException.ThrowIfNull(property);

        if (reservation.PropertyId != property.Id)
        {
            throw new ArgumentException("Reservation must belong to the supplied property.", nameof(property));
        }

        var propertyTimeZone = ResolvePropertyTimeZone(property.TimeZone);
        var currentLocalDateTime = TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), propertyTimeZone);
        var currentLocalDate = DateOnly.FromDateTime(currentLocalDateTime.DateTime);
        var preArrivalWindowDays = options.Value.PreArrivalWindowDays;
        var stage = DetermineStage(reservation, currentLocalDate, preArrivalWindowDays);

        return new ReservationLifecycleContext
        {
            ReservationId = reservation.Id,
            CompanyId = reservation.CompanyId,
            PropertyId = reservation.PropertyId,
            GuestId = reservation.PrimaryGuestId,
            LifecycleStage = stage,
            CheckInLocal = reservation.CheckInDate,
            CheckOutLocal = reservation.CheckOutDate,
            DaysUntilCheckIn = reservation.CheckInDate.DayNumber - currentLocalDate.DayNumber,
            DaysUntilCheckOut = reservation.CheckOutDate.DayNumber - currentLocalDate.DayNumber,
            IsCurrentlyInStay = stage is ReservationLifecycleStage.ArrivingToday or ReservationLifecycleStage.InStay or ReservationLifecycleStage.CheckingOutToday,
            PropertyTimeZone = propertyTimeZone.Id,
            CurrentLocalDateTime = currentLocalDateTime,
            PreArrivalWindowDays = preArrivalWindowDays
        };
    }

    private static ReservationLifecycleStage DetermineStage(Reservation reservation, DateOnly currentLocalDate, int preArrivalWindowDays)
    {
        return reservation.Status switch
        {
            ReservationStatus.Cancelled => ReservationLifecycleStage.Cancelled,
            ReservationStatus.NoShow => ReservationLifecycleStage.NoShow,
            ReservationStatus.Draft or ReservationStatus.PendingConfirmation => ReservationLifecycleStage.NotConfirmed,
            ReservationStatus.Completed => ReservationLifecycleStage.Completed,
            _ => DetermineTemporalStage(reservation, currentLocalDate, preArrivalWindowDays)
        };
    }

    private static ReservationLifecycleStage DetermineTemporalStage(Reservation reservation, DateOnly currentLocalDate, int preArrivalWindowDays)
    {
        if (currentLocalDate < reservation.CheckInDate.AddDays(-preArrivalWindowDays))
        {
            return ReservationLifecycleStage.FutureConfirmed;
        }

        if (currentLocalDate < reservation.CheckInDate)
        {
            return ReservationLifecycleStage.PreArrival;
        }

        if (currentLocalDate == reservation.CheckInDate)
        {
            return ReservationLifecycleStage.ArrivingToday;
        }

        if (currentLocalDate < reservation.CheckOutDate)
        {
            return ReservationLifecycleStage.InStay;
        }

        if (currentLocalDate == reservation.CheckOutDate)
        {
            return ReservationLifecycleStage.CheckingOutToday;
        }

        return ReservationLifecycleStage.Completed;
    }

    private static TimeZoneInfo ResolvePropertyTimeZone(string? propertyTimeZone)
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