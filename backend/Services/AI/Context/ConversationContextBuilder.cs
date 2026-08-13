using Microsoft.Extensions.Options;
using StayFlow.Api.DTOs.Conversations;
using StayFlow.Api.Models;
using StayFlow.Api.Repositories;

namespace StayFlow.Api.Services.AI.Context;

public sealed class ConversationContextBuilder(
    IConversationRepository conversationRepository,
    IPropertyKnowledgeRepository propertyKnowledgeRepository,
    IOptions<ConversationContextLimits> limitsOptions,
    ILogger<ConversationContextBuilder> logger) : IConversationContextBuilder
{
    public async Task<ConversationContext?> BuildAsync(Guid companyId, Guid conversationId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var limits = limitsOptions.Value;
        var conversation = await conversationRepository.GetByIdForCompanyAsync(companyId, conversationId, cancellationToken);
        if (conversation is null)
        {
            return null;
        }

        var history = await conversationRepository.GetMessagesAsync(companyId, conversationId, new ConversationHistoryQueryParameters
        {
            IncludeInternal = false,
            PageNumber = 1,
            PageSize = limits.ContextScanPageSize
        }, cancellationToken);

        var sourceMetadata = new List<ConversationContextSource>();
        var warnings = new HashSet<ConversationContextWarning>();

        var visibleMessages = BuildVisibleMessages(history.Items, limits, warnings, out var messageChars, out var messagesTruncated);
        var approvedKnowledge = await LoadApprovedKnowledgeAsync(companyId, conversation.PropertyId, cancellationToken);
        var knowledgeItems = BuildKnowledgeItems(approvedKnowledge, limits, warnings, out var knowledgeChars, out var knowledgeTruncated);

        var totalContextChars = messageChars + knowledgeChars;
        var totalCharsTruncated = false;

        if (totalContextChars > limits.MaxTotalPromptContextCharacters)
        {
            TrimForTotalCharacterLimit(visibleMessages, knowledgeItems, limits.MaxTotalPromptContextCharacters, out messageChars, out knowledgeChars);
            totalCharsTruncated = true;
            warnings.Add(ConversationContextWarning.ContextTruncated);
        }

        sourceMetadata.Add(new ConversationContextSource(
            ConversationContextSourceType.Conversation,
            null,
            BuildConversationTitle(conversation),
            null,
            conversation.UpdatedAt,
            "Conversation metadata and visible message history.",
            true));

        if (conversation.Reservation is not null)
        {
            sourceMetadata.Add(new ConversationContextSource(
                ConversationContextSourceType.Reservation,
                null,
                BuildReservationTitle(conversation.Reservation),
                null,
                conversation.Reservation.UpdatedAt,
                "Reservation details are linked to this conversation.",
                true));
        }
        else
        {
            warnings.Add(ConversationContextWarning.MissingReservation);
        }

        if (conversation.Property is not null)
        {
            sourceMetadata.Add(new ConversationContextSource(
                ConversationContextSourceType.Property,
                null,
                conversation.Property.Name,
                null,
                conversation.Property.UpdatedAt,
                "Property details are linked to this conversation.",
                true));
        }
        else
        {
            warnings.Add(ConversationContextWarning.MissingProperty);
        }

        foreach (var item in knowledgeItems)
        {
            sourceMetadata.Add(new ConversationContextSource(
                ConversationContextSourceType.PropertyKnowledge,
                item.SourceId,
                item.Title,
                item.Category.ToString(),
                item.LastUpdated,
                "Approved property knowledge available for AI grounding.",
                item.IsApproved));
        }

        if (knowledgeItems.Count == 0)
        {
            warnings.Add(ConversationContextWarning.NoApprovedKnowledge);
        }

        if (visibleMessages.Count == 0)
        {
            warnings.Add(ConversationContextWarning.NoVisibleMessages);
        }

        var requiresHostAttention = conversation.HumanTakeoverEnabled
            || conversation.Status == ConversationStatus.AwaitingHost
            || conversation.Status == ConversationStatus.Escalated
            || conversation.Status == ConversationStatus.HumanManaged;

        var contextTruncated = messagesTruncated || knowledgeTruncated || totalCharsTruncated;
        if (contextTruncated)
        {
            warnings.Add(ConversationContextWarning.ContextTruncated);
        }

        var guestName = $"{conversation.Guest.FirstName} {conversation.Guest.LastName}".Trim();
        if (string.IsNullOrWhiteSpace(guestName))
        {
            guestName = "Guest";
        }

        logger.LogInformation(
            "Conversation AI context resolved. ConversationId={ConversationId} CompanyId={CompanyId} PropertyId={PropertyId} ReservationId={ReservationId} ApprovedKnowledgeCount={ApprovedKnowledgeCount} VisibleMessageCount={VisibleMessageCount} HumanTakeoverEnabled={HumanTakeoverEnabled} RequiresHostAttention={RequiresHostAttention} Truncated={Truncated}",
            conversationId,
            companyId,
            conversation.PropertyId,
            conversation.ReservationId,
            knowledgeItems.Count,
            visibleMessages.Count,
            conversation.HumanTakeoverEnabled,
            requiresHostAttention,
            contextTruncated);

        return new ConversationContext(
            conversationId,
            companyId,
            conversation.Status.ToString(),
            conversation.Channel.ToString(),
            conversation.Subject,
            requiresHostAttention,
            conversation.HumanTakeoverEnabled,
            conversation.AssignedUser?.FullName,
            guestName,
            conversation.Guest.Email,
            conversation.PropertyId,
            conversation.Property?.Name,
            conversation.ReservationId,
            conversation.Reservation?.ConfirmationNumber,
            conversation.Reservation?.CheckInDate,
            conversation.Reservation?.CheckOutDate,
            conversation.Reservation?.Status.ToString(),
            visibleMessages,
            knowledgeItems,
            sourceMetadata,
            warnings.ToList(),
            contextTruncated,
            DateTimeOffset.UtcNow);
    }

    private static List<ConversationContextVisibleMessage> BuildVisibleMessages(
        IReadOnlyCollection<ConversationMessage> allMessages,
        ConversationContextLimits limits,
        HashSet<ConversationContextWarning> warnings,
        out int charCount,
        out bool truncated)
    {
        var ordered = allMessages
            .Where(message => !message.IsDeleted && !message.IsInternal)
            .OrderBy(message => message.SentAt)
            .ThenBy(message => message.CreatedAt)
            .ToList();

        var earliestGuest = ordered.FirstOrDefault(message => message.SenderType == ConversationSenderType.Guest);
        var selected = ordered.TakeLast(limits.MaxVisibleMessages).ToList();

        if (earliestGuest is not null && !selected.Any(message => message.Id == earliestGuest.Id) && selected.Count > 0)
        {
            selected[0] = earliestGuest;
            selected = selected
                .DistinctBy(message => message.Id)
                .OrderBy(message => message.SentAt)
                .ThenBy(message => message.CreatedAt)
                .TakeLast(limits.MaxVisibleMessages)
                .ToList();
        }

        truncated = ordered.Count > selected.Count;
        if (truncated)
        {
            warnings.Add(ConversationContextWarning.ContextTruncated);
        }

        var mapped = new List<ConversationContextVisibleMessage>(selected.Count);
        charCount = 0;

        foreach (var message in selected)
        {
            var normalized = NormalizeWhitespace(message.Content);
            if (normalized.Length > limits.MaxMessageCharacters)
            {
                normalized = normalized[..limits.MaxMessageCharacters];
                truncated = true;
                warnings.Add(ConversationContextWarning.ContextTruncated);
            }

            charCount += normalized.Length;
            mapped.Add(new ConversationContextVisibleMessage(
                message.Id.ToString("N"),
                message.SenderType.ToString(),
                message.SentAt.ToUniversalTime(),
                normalized));
        }

        return mapped;
    }

    private static List<ConversationContextKnowledgeItem> BuildKnowledgeItems(
        IReadOnlyCollection<PropertyKnowledgeArticle> knowledgeArticles,
        ConversationContextLimits limits,
        HashSet<ConversationContextWarning> warnings,
        out int charCount,
        out bool truncated)
    {
        charCount = 0;
        truncated = false;

        if (knowledgeArticles.Count == 0)
        {
            return [];
        }

        var ordered = knowledgeArticles
            .OrderByDescending(article => article.Priority)
            .ThenByDescending(article => article.UpdatedAt.UtcDateTime)
            .ThenBy(article => article.Title)
            .ToList();

        var selected = ordered.Take(limits.MaxKnowledgeItems).ToList();
        if (ordered.Count > selected.Count)
        {
            truncated = true;
            warnings.Add(ConversationContextWarning.ContextTruncated);
        }

        var mapped = new List<ConversationContextKnowledgeItem>(selected.Count);
        foreach (var item in selected)
        {
            var title = NormalizeWhitespace(item.Title);
            var content = NormalizeKnowledgeContent(item.Content);
            if (string.IsNullOrWhiteSpace(content))
            {
                continue;
            }

            if (content.Length > limits.MaxKnowledgeItemCharacters)
            {
                content = content[..limits.MaxKnowledgeItemCharacters];
                truncated = true;
                warnings.Add(ConversationContextWarning.ContextTruncated);
            }

            charCount += content.Length;
            mapped.Add(new ConversationContextKnowledgeItem(
                item.Id.ToString("N"),
                title,
                content,
                item.Category,
                item.UpdatedAt.ToUniversalTime(),
                item.Priority,
                item.IsApproved,
                NormalizeTags(item.Tags),
                NormalizeOptional(item.Summary)));
        }

        return mapped;
    }

    private async Task<IReadOnlyCollection<PropertyKnowledgeArticle>> LoadApprovedKnowledgeAsync(
        Guid companyId,
        Guid? propertyId,
        CancellationToken cancellationToken)
    {
        if (propertyId is not { } resolvedPropertyId)
        {
            return [];
        }

        var items = await propertyKnowledgeRepository.GetApprovedActiveForPropertyAsync(
            companyId,
            resolvedPropertyId,
            cancellationToken);

        return items
            .Where(item => item.CompanyId == companyId
                && item.PropertyId == resolvedPropertyId
                && item.IsApproved
                && item.IsActive
                && !item.IsDeleted)
            .OrderByDescending(item => item.Priority)
            .ThenByDescending(item => item.UpdatedAt.UtcDateTime)
            .ThenBy(item => item.Title)
            .ToList();
    }

    private static void TrimForTotalCharacterLimit(
        List<ConversationContextVisibleMessage> messages,
        List<ConversationContextKnowledgeItem> knowledgeItems,
        int maxTotal,
        out int messageChars,
        out int knowledgeChars)
    {
        messageChars = messages.Sum(message => message.Text.Length);
        knowledgeChars = knowledgeItems.Sum(item => item.Content.Length);

        while (messageChars + knowledgeChars > maxTotal && knowledgeItems.Count > 0)
        {
            var lastIndex = knowledgeItems.Count - 1;
            knowledgeChars -= knowledgeItems[lastIndex].Content.Length;
            knowledgeItems.RemoveAt(lastIndex);
        }

        while (messageChars + knowledgeChars > maxTotal && messages.Count > 1)
        {
            var removeIndex = 0;
            messageChars -= messages[removeIndex].Text.Length;
            messages.RemoveAt(removeIndex);
        }

        if (messageChars + knowledgeChars > maxTotal && messages.Count == 1)
        {
            var only = messages[0];
            var remaining = Math.Max(0, maxTotal - knowledgeChars);
            var trimmed = only.Text.Length <= remaining ? only.Text : only.Text[..remaining];
            messageChars -= only.Text.Length;
            messageChars += trimmed.Length;
            messages[0] = only with { Text = trimmed };
        }
    }

    private static string BuildConversationTitle(Conversation conversation)
    {
        return string.IsNullOrWhiteSpace(conversation.Subject)
            ? "Conversation"
            : conversation.Subject.Trim();
    }

    private static string BuildReservationTitle(Reservation reservation)
    {
        return string.IsNullOrWhiteSpace(reservation.ConfirmationNumber)
            ? "Reservation"
            : $"Reservation {reservation.ConfirmationNumber.Trim()}";
    }

    private static IReadOnlyCollection<string> NormalizeTags(string? tags)
    {
        return string.IsNullOrWhiteSpace(tags)
            ? []
            : tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(NormalizeWhitespace)
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : NormalizeWhitespace(value);
    }

    private static string NormalizeWhitespace(string value)
    {
        return string.Join(' ', value
            .Split(['\r', '\n', '\t', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private static string NormalizeKnowledgeContent(string value)
    {
        var lines = value
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n')
            .Select(line => string.Join(' ', line.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)))
            .ToList();

        var compact = new List<string>(lines.Count);
        var previousBlank = false;
        foreach (var line in lines)
        {
            var isBlank = string.IsNullOrWhiteSpace(line);
            if (isBlank && previousBlank)
            {
                continue;
            }

            compact.Add(isBlank ? string.Empty : line);
            previousBlank = isBlank;
        }

        return string.Join("\n", compact).Trim();
    }
}
