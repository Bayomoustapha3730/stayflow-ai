using Microsoft.Extensions.Options;
using StayFlow.Api.Models;
using StayFlow.Api.Repositories;

namespace StayFlow.Api.Services;

public sealed class ReservationLifecycleEventGenerator(
    IReservationLifecycleEventRepository repository,
    IReservationLifecycleEventService lifecycleEventService,
    IReservationLifecycleEventIdempotencyKeyBuilder idempotencyKeyBuilder,
    TimeProvider timeProvider,
    IOptions<ReservationLifecycleEventOptions> lifecycleEventOptions,
    IOptions<ReservationContextOptions> reservationContextOptions,
    ILogger<ReservationLifecycleEventGenerator> logger) : IReservationLifecycleEventGenerator
{
    public async Task<int> GenerateAsync(CancellationToken cancellationToken)
    {
        var options = lifecycleEventOptions.Value;
        var nowUtc = timeProvider.GetUtcNow();
        var utcDate = DateOnly.FromDateTime(nowUtc.UtcDateTime);
        var preArrivalWindowDays = reservationContextOptions.Value.PreArrivalWindowDays;
        var queryStart = utcDate.AddDays(-options.GenerationLookbackDays - 1);
        var queryEnd = utcDate.AddDays(options.GenerationHorizonDays + preArrivalWindowDays + 1);

        var reservations = await repository.GetGenerationCandidatesAsync(queryStart, queryEnd, options.GenerationBatchSize, cancellationToken);
        var created = 0;

        foreach (var reservation in reservations)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var property = reservation.Property;
            if (property is null || reservation.CompanyId != property.CompanyId || reservation.PropertyId != property.Id)
            {
                logger.LogWarning(
                    "Reservation lifecycle generation skipped reservation {ReservationId} because its property relationship is invalid. CompanyId={CompanyId} PropertyId={PropertyId}.",
                    reservation.Id,
                    reservation.CompanyId,
                    reservation.PropertyId);
                continue;
            }

            DateOnly windowStart;
            DateOnly windowEnd;
            try
            {
                var propertyTimeZone = PropertyTimeZoneResolver.Resolve(property.TimeZone);
                var propertyLocalNow = TimeZoneInfo.ConvertTime(nowUtc, propertyTimeZone);
                var propertyLocalToday = DateOnly.FromDateTime(propertyLocalNow.DateTime);
                windowStart = propertyLocalToday.AddDays(-options.GenerationLookbackDays);
                windowEnd = propertyLocalToday.AddDays(options.GenerationHorizonDays);
            }
            catch (ArgumentException ex)
            {
                logger.LogWarning(
                    ex,
                    "Reservation lifecycle generation skipped reservation {ReservationId} because property timezone is invalid. CompanyId={CompanyId} PropertyId={PropertyId}.",
                    reservation.Id,
                    reservation.CompanyId,
                    reservation.PropertyId);
                continue;
            }

            var anchors = BuildAnchors(reservation, preArrivalWindowDays).ToList();
            var currentKeys = anchors
                .Select(anchor => idempotencyKeyBuilder.Build(reservation.CompanyId, reservation.Id, anchor.EventType, anchor.PropertyLocalDate, ReservationLifecycleRuleVersions.V1))
                .ToList();

            await repository.SuppressObsoleteUnprocessedAsync(
                reservation.CompanyId,
                reservation.Id,
                currentKeys,
                nowUtc,
                "Reservation lifecycle event no longer matches current reservation dates/rules.",
                cancellationToken);

            foreach (var anchor in anchors.Where(anchor => anchor.PropertyLocalDate >= windowStart && anchor.PropertyLocalDate <= windowEnd))
            {
                var result = await lifecycleEventService.TryCreateAsync(reservation, property, anchor.EventType, anchor.PropertyLocalDate, cancellationToken);
                if (result.WasNewlyCreated)
                {
                    created++;
                }
            }
        }

        return created;
    }

    internal static IEnumerable<ReservationLifecycleEventAnchor> BuildAnchors(Reservation reservation, int preArrivalWindowDays)
    {
        yield return new ReservationLifecycleEventAnchor(ReservationLifecycleEventType.PreArrival, reservation.CheckInDate.AddDays(-preArrivalWindowDays));
        yield return new ReservationLifecycleEventAnchor(ReservationLifecycleEventType.ArrivalDay, reservation.CheckInDate);

        var inStayDate = reservation.CheckInDate.AddDays(1);
        if (inStayDate < reservation.CheckOutDate)
        {
            yield return new ReservationLifecycleEventAnchor(ReservationLifecycleEventType.InStay, inStayDate);
        }

        yield return new ReservationLifecycleEventAnchor(ReservationLifecycleEventType.CheckoutDay, reservation.CheckOutDate);
        yield return new ReservationLifecycleEventAnchor(ReservationLifecycleEventType.PostStay, reservation.CheckOutDate.AddDays(1));
    }
}

public sealed record ReservationLifecycleEventAnchor(ReservationLifecycleEventType EventType, DateOnly PropertyLocalDate);