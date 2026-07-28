using Microsoft.EntityFrameworkCore;
using StayFlow.Api.Common;
using StayFlow.Api.Data;
using StayFlow.Api.DTOs.WhatsApp;
using StayFlow.Api.Models;

namespace StayFlow.Api.Repositories;

public sealed class WhatsAppRepository(ApplicationDbContext dbContext) : IWhatsAppRepository
{
    public async Task<IReadOnlyCollection<WhatsAppIntegration>> ListActiveIntegrationsAsync(CancellationToken cancellationToken)
    {
        return await dbContext.WhatsAppIntegrations
            .Where(integration => integration.IsActive)
            .OrderBy(integration => integration.CreatedAt)
            .ToListAsync(cancellationToken);
    }

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

    public Task<WhatsAppIntegration?> GetIntegrationForCompanyAsync(Guid companyId, Guid integrationId, CancellationToken cancellationToken)
    {
        return dbContext.WhatsAppIntegrations
            .FirstOrDefaultAsync(integration => integration.CompanyId == companyId && integration.Id == integrationId, cancellationToken);
    }

    public async Task<IReadOnlyCollection<WhatsAppIntegration>> ListIntegrationsForCompanyAsync(Guid companyId, CancellationToken cancellationToken)
    {
        return await dbContext.WhatsAppIntegrations
            .Where(integration => integration.CompanyId == companyId)
            .OrderByDescending(integration => integration.IsActive)
            .ThenBy(integration => integration.DisplayName)
            .ToListAsync(cancellationToken);
    }

    public async Task<PagedResult<WhatsAppTemplate>> ListTemplatesAsync(Guid companyId, Guid integrationId, WhatsAppTemplateListQuery query, CancellationToken cancellationToken)
    {
        var page = query.NormalizedPageNumber;
        var pageSize = query.NormalizedPageSize;

        var templates = dbContext.WhatsAppTemplates
            .AsNoTracking()
            .Where(template => template.CompanyId == companyId && template.WhatsAppIntegrationId == integrationId);

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var status = query.Status.Trim();
            templates = templates.Where(template => template.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(query.Language))
        {
            var language = query.Language.Trim();
            templates = templates.Where(template => template.LanguageCode == language);
        }

        if (!string.IsNullOrWhiteSpace(query.Category))
        {
            var category = query.Category.Trim();
            templates = templates.Where(template => template.Category == category);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            templates = templates.Where(template =>
                EF.Functions.ILike(template.Name, $"%{search}%")
                || EF.Functions.ILike(template.BodyText, $"%{search}%"));
        }

        if (query.Active is { } active)
        {
            templates = templates.Where(template => template.IsActive == active);
        }

        if (query.ApprovedOnly == true)
        {
            templates = templates.Where(template => template.Status == "APPROVED");
        }

        var totalCount = await templates.CountAsync(cancellationToken);
        var items = await templates
            .OrderBy(template => template.Name)
            .ThenBy(template => template.LanguageCode)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<WhatsAppTemplate>
        {
            Items = items,
            PageNumber = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public Task<WhatsAppTemplate?> GetTemplateForCompanyAsync(Guid companyId, Guid integrationId, Guid templateId, CancellationToken cancellationToken)
    {
        return dbContext.WhatsAppTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(template => template.CompanyId == companyId
                && template.WhatsAppIntegrationId == integrationId
                && template.Id == templateId, cancellationToken);
    }

    public Task<WhatsAppTemplate?> GetTemplateByNameAsync(Guid companyId, Guid integrationId, string name, string languageCode, CancellationToken cancellationToken)
    {
        return dbContext.WhatsAppTemplates
            .FirstOrDefaultAsync(template => template.CompanyId == companyId
                && template.WhatsAppIntegrationId == integrationId
                && template.Name == name
                && template.LanguageCode == languageCode, cancellationToken);
    }

    public async Task<IReadOnlyCollection<WhatsAppTemplate>> ListTemplatesForIntegrationAsync(Guid companyId, Guid integrationId, CancellationToken cancellationToken)
    {
        return await dbContext.WhatsAppTemplates
            .Where(template => template.CompanyId == companyId && template.WhatsAppIntegrationId == integrationId)
            .ToListAsync(cancellationToken);
    }

    public Task<ConversationMessage?> GetLatestInboundGuestWhatsAppMessageAsync(Guid companyId, Guid conversationId, CancellationToken cancellationToken)
    {
        return dbContext.ConversationMessages
            .AsNoTracking()
            .Where(message => message.CompanyId == companyId
                && message.ConversationId == conversationId
                && !message.IsDeleted
                && message.Provider == ConversationMessageProvider.WhatsAppCloud
                && message.SenderType == ConversationSenderType.Guest)
            .OrderByDescending(message => message.SentAt)
            .FirstOrDefaultAsync(cancellationToken);
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

    public async Task AddTemplateAsync(WhatsAppTemplate template, CancellationToken cancellationToken)
    {
        await dbContext.WhatsAppTemplates.AddAsync(template, cancellationToken);
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