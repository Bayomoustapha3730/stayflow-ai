using Microsoft.EntityFrameworkCore;
using StayFlow.Api.Data;
using StayFlow.Api.Models;

namespace StayFlow.Api.Repositories;

public sealed class AIContextRepository(ApplicationDbContext dbContext) : IAIContextRepository
{
    public Task<Guest?> GetGuestAsync(Guid companyId, Guid guestId, CancellationToken cancellationToken)
    {
        return dbContext.Guests
            .AsNoTracking()
            .FirstOrDefaultAsync(guest => guest.Id == guestId && guest.CompanyId == companyId && guest.IsActive, cancellationToken);
    }

    public Task<int> CountCompletedReservationsForGuestAsync(Guid companyId, Guid guestId, CancellationToken cancellationToken)
    {
        return dbContext.Reservations
            .AsNoTracking()
            .CountAsync(reservation =>
                reservation.CompanyId == companyId
                && reservation.PrimaryGuestId == guestId
                && reservation.Status == ReservationStatus.Completed,
                cancellationToken);
    }

    public Task<Reservation?> GetReservationAsync(Guid companyId, Guid reservationId, CancellationToken cancellationToken)
    {
        return dbContext.Reservations
            .AsNoTracking()
            .FirstOrDefaultAsync(reservation => reservation.Id == reservationId && reservation.CompanyId == companyId && reservation.IsActive, cancellationToken);
    }

    public Task<Property?> GetPropertyContextAsync(Guid companyId, Guid propertyId, CancellationToken cancellationToken)
    {
        return dbContext.Properties
            .AsNoTracking()
            .Include(property => property.PropertyAmenities)
            .Include(property => property.PropertyHouseRules)
            .Include(property => property.PropertyRecommendations)
            .Include(property => property.PropertyEmergencyContacts)
            .Include(property => property.PropertyKnowledgeArticles)
            .FirstOrDefaultAsync(property => property.Id == propertyId && property.CompanyId == companyId && property.IsActive, cancellationToken);
    }

    public Task<Conversation?> GetConversationAsync(Guid companyId, Guid conversationId, CancellationToken cancellationToken)
    {
        return dbContext.Conversations
            .AsNoTracking()
            .FirstOrDefaultAsync(conversation => conversation.Id == conversationId && conversation.CompanyId == companyId, cancellationToken);
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
