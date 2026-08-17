using Microsoft.EntityFrameworkCore;
using StayFlow.Api.Data;
using StayFlow.Api.Models;

namespace StayFlow.Api.Repositories;

public sealed class PaymentRepository(ApplicationDbContext dbContext) : IPaymentRepository
{
    public Task<Reservation?> GetReservationForPaymentAsync(Guid reservationId, Guid companyId, CancellationToken cancellationToken)
    {
        return dbContext.Reservations
            .Include(reservation => reservation.Property)
            .Include(reservation => reservation.PrimaryGuest)
            .FirstOrDefaultAsync(
                reservation => reservation.Id == reservationId && reservation.CompanyId == companyId && !reservation.IsDeleted,
                cancellationToken);
    }

    public Task<Payment?> GetByExternalReferenceAsync(string externalReference, Guid companyId, CancellationToken cancellationToken)
    {
        return dbContext.Payments.FirstOrDefaultAsync(
            payment => payment.ExternalReference == externalReference && payment.CompanyId == companyId,
            cancellationToken);
    }

    public Task<Payment?> GetByIdAsync(Guid id, Guid companyId, CancellationToken cancellationToken)
    {
        return dbContext.Payments
            .FirstOrDefaultAsync(payment => payment.Id == id && payment.CompanyId == companyId, cancellationToken);
    }

    public async Task<IReadOnlyCollection<Payment>> GetByReservationIdAsync(Guid reservationId, Guid companyId, CancellationToken cancellationToken)
    {
        return await dbContext.Payments
            .AsNoTracking()
            .Where(payment => payment.ReservationId == reservationId && payment.CompanyId == companyId)
            .OrderByDescending(payment => payment.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public Task<Payment?> GetByCheckoutRequestIdAsync(string checkoutRequestId, CancellationToken cancellationToken)
    {
        return dbContext.Payments
            .FirstOrDefaultAsync(payment => payment.ProviderCheckoutRequestId == checkoutRequestId, cancellationToken);
    }

    public async Task<IReadOnlyCollection<Payment>> GetStaleMpesaPaymentsAsync(
        DateTimeOffset requestedBeforeUtc,
        int take,
        CancellationToken cancellationToken)
    {
        return await dbContext.Payments
            .Where(payment =>
                payment.Provider == "M-PESA"
                && (payment.Status == "Pending" || payment.Status == "Processing")
                && payment.ProviderCheckoutRequestId != null
                && payment.RequestedAtUtc != null
                && payment.RequestedAtUtc <= requestedBeforeUtc)
            .OrderBy(payment => payment.RequestedAtUtc)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public Task<bool> ReservationBelongsToCompanyAsync(Guid reservationId, Guid companyId, CancellationToken cancellationToken)
    {
        return dbContext.Reservations.AnyAsync(
            reservation => reservation.Id == reservationId && reservation.CompanyId == companyId,
            cancellationToken);
    }

    public async Task AddAsync(Payment payment, CancellationToken cancellationToken)
    {
        await dbContext.Payments.AddAsync(payment, cancellationToken);
    }

    public async Task<bool> TryRecordWebhookEventAsync(PaymentWebhookEvent webhookEvent, CancellationToken cancellationToken)
    {
        var alreadyProcessed = await dbContext.PaymentWebhookEvents.AnyAsync(
            existing => existing.Provider == webhookEvent.Provider && existing.EventId == webhookEvent.EventId,
            cancellationToken);

        if (alreadyProcessed)
        {
            return false;
        }

        await dbContext.PaymentWebhookEvents.AddAsync(webhookEvent, cancellationToken);
        return true;
    }

    public async Task AddAuditLogAsync(AuditLog auditLog, CancellationToken cancellationToken)
    {
        await dbContext.AuditLogs.AddAsync(auditLog, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }
}
