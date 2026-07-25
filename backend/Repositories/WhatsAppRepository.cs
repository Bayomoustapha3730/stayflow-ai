using Microsoft.EntityFrameworkCore;
using StayFlow.Api.Data;
using StayFlow.Api.Models;

namespace StayFlow.Api.Repositories;

public sealed class WhatsAppRepository(ApplicationDbContext dbContext) : IWhatsAppRepository
{
    public Task<WhatsAppIntegration?> GetActiveIntegrationByPhoneNumberIdAsync(string phoneNumberId, CancellationToken cancellationToken)
    {
        return dbContext.WhatsAppIntegrations
            .Include(integration => integration.Company)
            .FirstOrDefaultAsync(integration => integration.PhoneNumberId == phoneNumberId && integration.IsActive && integration.Company.IsActive, cancellationToken);
    }

    public Task<WhatsAppIntegration?> GetActiveIntegrationByCompanyIdAsync(Guid companyId, CancellationToken cancellationToken)
    {
        return dbContext.WhatsAppIntegrations
            .FirstOrDefaultAsync(integration => integration.CompanyId == companyId && integration.IsActive, cancellationToken);
    }

    public async Task<IReadOnlyCollection<Guest>> ListActiveGuestsWithPhoneAsync(Guid companyId, CancellationToken cancellationToken)
    {
        return await dbContext.Guests
            .Where(guest => guest.CompanyId == companyId && guest.IsActive && !guest.IsDeleted && guest.PhoneNumber != null)
            .OrderBy(guest => guest.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<Reservation>> GetEligibleReservationsForGuestAsync(Guid companyId, Guid guestId, DateOnly currentDate, DateOnly upcomingThroughDate, CancellationToken cancellationToken)
    {
        return await dbContext.Reservations
            .Include(reservation => reservation.Property)
            .Where(reservation => reservation.CompanyId == companyId && reservation.PrimaryGuestId == guestId)
            .Where(reservation => reservation.IsActive && !reservation.IsDeleted)
            .Where(reservation => reservation.Status != ReservationStatus.Cancelled && reservation.Status != ReservationStatus.NoShow)
            .Where(reservation =>
                ((reservation.Status == ReservationStatus.ReadyForCheckIn
                    || reservation.Status == ReservationStatus.CheckedIn
                    || reservation.Status == ReservationStatus.ActiveStay
                    || reservation.Status == ReservationStatus.CheckOutPending)
                    && reservation.CheckInDate <= currentDate
                    && reservation.CheckOutDate >= currentDate)
                || ((reservation.Status == ReservationStatus.Confirmed
                    || reservation.Status == ReservationStatus.PreArrival
                    || reservation.Status == ReservationStatus.ReadyForCheckIn)
                    && reservation.CheckInDate >= currentDate
                    && reservation.CheckInDate <= upcomingThroughDate))
            .OrderBy(reservation => reservation.CheckInDate)
            .ToListAsync(cancellationToken);
    }

    public Task<ConversationMessage?> FindMessageByProviderExternalIdAsync(Guid companyId, ConversationMessageProvider provider, string externalMessageId, CancellationToken cancellationToken)
    {
        return dbContext.ConversationMessages
            .Include(message => message.Conversation)
            .FirstOrDefaultAsync(message => message.CompanyId == companyId && message.Provider == provider && message.ExternalMessageId == externalMessageId, cancellationToken);
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