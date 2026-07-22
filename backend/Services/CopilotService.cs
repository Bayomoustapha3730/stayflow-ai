using System.Text;
using StayFlow.Api.Common;
using StayFlow.Api.DTOs.AIPrompt;
using StayFlow.Api.DTOs.AIProvider;
using StayFlow.Api.DTOs.Copilot;
using StayFlow.Api.DTOs.Conversations;
using StayFlow.Api.Models;
using StayFlow.Api.Repositories;

namespace StayFlow.Api.Services;

public sealed class CopilotService(
    IConversationRepository conversationRepository,
    ICurrentTenantContext currentTenantContext,
    IAIProvider aiProvider) : ICopilotService
{
    private const int MinContextMessages = 4;
    private const int MaxContextMessages = 50;

    public async Task<ApiResponse<ConversationCopilotSummaryResponse>> GetSummaryAsync(
        Guid conversationId,
        CancellationToken cancellationToken)
    {
        if (!TryGetCompanyId(out var companyId, out var tenantError))
        {
            return ApiResponse<ConversationCopilotSummaryResponse>.Fail(tenantError, [tenantError]);
        }

        var conversation = await conversationRepository.GetByIdForCompanyAsync(companyId, conversationId, cancellationToken);
        if (conversation is null)
        {
            return ApiResponse<ConversationCopilotSummaryResponse>.Fail("Conversation was not found.");
        }

        var visibleMessages = await GetVisibleMessagesAsync(companyId, conversationId, cancellationToken);
        var latestGuestMessage = visibleMessages.LastOrDefault(message => message.SenderType == ConversationSenderType.Guest);
        var summary = BuildDeterministicSummary(conversation, visibleMessages, latestGuestMessage);

        return ApiResponse<ConversationCopilotSummaryResponse>.Ok(new ConversationCopilotSummaryResponse
        {
            ConversationId = conversationId,
            Summary = summary,
            LatestGuestMessage = latestGuestMessage?.Content,
            VisibleMessageCount = visibleMessages.Count,
            GeneratedAt = DateTimeOffset.UtcNow
        });
    }

    public async Task<ApiResponse<ConversationCopilotSuggestionsResponse>> GetSuggestedRepliesAsync(
        Guid conversationId,
        CancellationToken cancellationToken)
    {
        if (!TryGetCompanyId(out var companyId, out var tenantError))
        {
            return ApiResponse<ConversationCopilotSuggestionsResponse>.Fail(tenantError, [tenantError]);
        }

        var conversation = await conversationRepository.GetByIdForCompanyAsync(companyId, conversationId, cancellationToken);
        if (conversation is null)
        {
            return ApiResponse<ConversationCopilotSuggestionsResponse>.Fail("Conversation was not found.");
        }

        var visibleMessages = await GetVisibleMessagesAsync(companyId, conversationId, cancellationToken);
        var latestGuestMessage = visibleMessages.LastOrDefault(message => message.SenderType == ConversationSenderType.Guest);

        return ApiResponse<ConversationCopilotSuggestionsResponse>.Ok(new ConversationCopilotSuggestionsResponse
        {
            ConversationId = conversationId,
            SuggestedReplies = BuildDeterministicSuggestedReplies(latestGuestMessage?.Content),
            ContextMessageCount = visibleMessages.Count,
            GeneratedAt = DateTimeOffset.UtcNow
        });
    }

    public async Task<ApiResponse<CopilotSuggestReplyResponse>> SuggestHostReplyAsync(
        Guid conversationId,
        CopilotSuggestReplyRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCompanyId(out var companyId, out var tenantError))
        {
            return ApiResponse<CopilotSuggestReplyResponse>.Fail(tenantError, [tenantError]);
        }

        var contextMessageCount = Math.Clamp(request.MaxContextMessages, MinContextMessages, MaxContextMessages);
        var conversation = await conversationRepository.GetByIdForCompanyAsync(companyId, conversationId, cancellationToken);
        if (conversation is null)
        {
            return ApiResponse<CopilotSuggestReplyResponse>.Fail("Conversation was not found.");
        }

        if (conversation.Status == ConversationStatus.Closed)
        {
            return ApiResponse<CopilotSuggestReplyResponse>.Fail("Conversation is closed and cannot be drafted.", ["Conversation is closed."]);
        }

        var history = await conversationRepository.GetMessagesAsync(companyId, conversationId, new ConversationHistoryQueryParameters
        {
            IncludeInternal = request.IncludeInternalNotes,
            PageNumber = 1,
            PageSize = MaxContextMessages
        }, cancellationToken);

        var contextMessages = history.Items
            .Where(message => !message.IsDeleted)
            .TakeLast(contextMessageCount)
            .ToList();
        var latestGuestMessage = contextMessages.LastOrDefault(message => message.SenderType == ConversationSenderType.Guest);
        var fallbackDraft = BuildFallbackDraft(conversation, latestGuestMessage, request.Guidance);

        var promptMessages = BuildPromptMessages(conversation, contextMessages, request.Guidance);
        if (promptMessages.Count == 0)
        {
            return ApiResponse<CopilotSuggestReplyResponse>.Ok(new CopilotSuggestReplyResponse
            {
                ConversationId = conversationId,
                SuggestedReply = fallbackDraft,
                Rationale = "Generated fallback draft because no conversation context was available.",
                ContextMessageCount = 0,
                IsFallback = true,
                GeneratedAt = DateTimeOffset.UtcNow
            });
        }

        AIProviderResult? providerResult;
        try
        {
            providerResult = await aiProvider.GenerateAsync(new AIProviderRequest
            {
                PromptPackage = new AIPromptPackage
                {
                    PreferredLanguage = conversation.Guest.PreferredLanguage,
                    GuestMessage = latestGuestMessage?.Content ?? string.Empty,
                    RenderedMessages = promptMessages,
                    ResponseConstraints = new AIResponseConstraints
                    {
                        PreferredLanguage = conversation.Guest.PreferredLanguage,
                        MaxResponseCharacters = 700,
                        AllowMarkdown = false,
                        GuestFriendlyTone = true,
                        RequiresEscalationWhenInsufficient = false
                    }
                },
                RenderedMessages = promptMessages,
                ResponseConstraints = new AIResponseConstraints
                {
                    PreferredLanguage = conversation.Guest.PreferredLanguage,
                    MaxResponseCharacters = 700,
                    AllowMarkdown = false,
                    GuestFriendlyTone = true,
                    RequiresEscalationWhenInsufficient = false
                },
                CorrelationId = currentTenantContext.CorrelationId
            }, cancellationToken);
        }
        catch
        {
            providerResult = null;
        }

        var generated = providerResult is not null
            && providerResult.Outcome == AIProviderOutcome.Success
            && !string.IsNullOrWhiteSpace(providerResult.ResponseText)
            ? NormalizeProviderResponse(providerResult.ResponseText)
            : fallbackDraft;

        var usedFallback = string.Equals(generated, fallbackDraft, StringComparison.Ordinal);
        return ApiResponse<CopilotSuggestReplyResponse>.Ok(new CopilotSuggestReplyResponse
        {
            ConversationId = conversationId,
            SuggestedReply = generated,
            Rationale = usedFallback
                ? "Generated fallback draft because the AI provider was unavailable or returned an empty response."
                : "Generated from recent conversation context and optional host guidance.",
            ContextMessageCount = contextMessages.Count,
            IsFallback = usedFallback,
            ProviderMetadata = providerResult is null
                ? null
                : new CopilotProviderMetadataResponse
                {
                    ProviderName = providerResult.ProviderName,
                    ModelName = providerResult.ModelName,
                    RequestId = providerResult.RequestId
                },
            GeneratedAt = DateTimeOffset.UtcNow
        });
    }

    private static List<AIPromptMessage> BuildPromptMessages(
        Conversation conversation,
        IReadOnlyCollection<ConversationMessage> contextMessages,
        string? guidance)
    {
        if (contextMessages.Count == 0)
        {
            return [];
        }

        var builder = new StringBuilder();
        builder.AppendLine("You are StayFlow Host Copilot.");
        builder.AppendLine("Write one concise host reply to the guest's latest concern.");
        builder.AppendLine("Rules:");
        builder.AppendLine("- Keep the tone warm, clear, and professional.");
        builder.AppendLine("- Do not invent policy, pricing, or property facts.");
        builder.AppendLine("- If more information is needed, ask one direct follow-up question.");
        builder.AppendLine("- Output only the suggested reply text.");

        if (!string.IsNullOrWhiteSpace(guidance))
        {
            builder.AppendLine($"Host guidance: {guidance.Trim()}");
        }

        var guestName = $"{conversation.Guest.FirstName} {conversation.Guest.LastName}".Trim();
        if (string.IsNullOrWhiteSpace(guestName))
        {
            guestName = "Guest";
        }

        builder.AppendLine($"Guest name: {guestName}");
        if (conversation.Property is not null)
        {
            builder.AppendLine($"Property: {conversation.Property.Name}");
        }

        builder.AppendLine("Recent conversation transcript:");
        foreach (var message in contextMessages)
        {
            builder.AppendLine($"[{message.SentAt:O}] {MapSenderLabel(message)}: {message.Content}");
        }

        return
        [
            new AIPromptMessage
            {
                Role = "system",
                Content = "You assist host agents in drafting accurate guest replies."
            },
            new AIPromptMessage
            {
                Role = "user",
                Content = builder.ToString().Trim()
            }
        ];
    }

    private static string MapSenderLabel(ConversationMessage message)
    {
        return message.SenderType switch
        {
            ConversationSenderType.Guest => "Guest",
            ConversationSenderType.Host => "Host",
            ConversationSenderType.AI => "Assistant",
            _ => "System"
        };
    }

    private static string BuildFallbackDraft(Conversation conversation, ConversationMessage? latestGuestMessage, string? guidance)
    {
        var firstName = string.IsNullOrWhiteSpace(conversation.Guest.FirstName)
            ? "there"
            : conversation.Guest.FirstName.Trim();
        var reference = latestGuestMessage is null
            ? "your message"
            : $"your note about \"{Truncate(latestGuestMessage.Content.Trim(), 90)}\"";
        var optionalGuidance = string.IsNullOrWhiteSpace(guidance)
            ? string.Empty
            : $" {Truncate(guidance.Trim(), 140)}";

        return $"Hi {firstName}, thanks for reaching out about {reference}. I am checking this now and will share a clear update shortly.{optionalGuidance}";
    }

    private static string NormalizeProviderResponse(string response)
    {
        var normalized = response.Trim().Trim('"');
        return string.IsNullOrWhiteSpace(normalized)
            ? "Thanks for your message. I will check this and get back to you shortly."
            : Truncate(normalized, 700);
    }

    private static string Truncate(string value, int maxLength)
    {
        if (value.Length <= maxLength)
        {
            return value;
        }

        return $"{value[..Math.Max(0, maxLength - 3)].TrimEnd()}...";
    }

    private bool TryGetCompanyId(out Guid companyId, out string error)
    {
        companyId = currentTenantContext.CompanyId ?? Guid.Empty;
        if (!currentTenantContext.IsAuthenticated || companyId == Guid.Empty)
        {
            error = "Authenticated tenant context is required.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private async Task<List<ConversationMessage>> GetVisibleMessagesAsync(Guid companyId, Guid conversationId, CancellationToken cancellationToken)
    {
        var history = await conversationRepository.GetMessagesAsync(companyId, conversationId, new ConversationHistoryQueryParameters
        {
            IncludeInternal = false,
            PageNumber = 1,
            PageSize = MaxContextMessages
        }, cancellationToken);

        return history.Items
            .Where(message => !message.IsDeleted && !message.IsInternal)
            .OrderBy(message => message.SentAt)
            .ThenBy(message => message.CreatedAt)
            .ToList();
    }

    private static string BuildDeterministicSummary(
        Conversation conversation,
        IReadOnlyCollection<ConversationMessage> visibleMessages,
        ConversationMessage? latestGuestMessage)
    {
        var guestName = $"{conversation.Guest.FirstName} {conversation.Guest.LastName}".Trim();
        if (string.IsNullOrWhiteSpace(guestName))
        {
            guestName = "Guest";
        }

        var propertyName = string.IsNullOrWhiteSpace(conversation.Property?.Name)
            ? "the property"
            : conversation.Property!.Name;
        var latestSnippet = latestGuestMessage is null
            ? "No guest message yet"
            : Truncate(latestGuestMessage.Content.Trim(), 120);

        return $"{guestName} conversation at {propertyName} is currently {conversation.Status}. Visible messages: {visibleMessages.Count}. Latest guest message: {latestSnippet}.";
    }

    private static IReadOnlyCollection<string> BuildDeterministicSuggestedReplies(string? latestGuestMessage)
    {
        var normalized = (latestGuestMessage ?? string.Empty).Trim().ToLowerInvariant();

        if (normalized.Contains("check-in") || normalized.Contains("check in"))
        {
            return
            [
                "Thanks for checking in. I can confirm your check-in details and send the exact arrival steps shortly.",
                "Happy to help with check-in. Could you confirm your expected arrival time so I can prepare the best guidance?",
                "I have received your check-in question and will share the updated instructions in a moment."
            ];
        }

        if (normalized.Contains("check-out") || normalized.Contains("checkout") || normalized.Contains("check out"))
        {
            return
            [
                "Thanks for your message. I will confirm the check-out process and timing for your reservation.",
                "I can help with check-out details. Are you asking about the time, luggage options, or both?",
                "Understood, I am reviewing your check-out request and will respond with clear next steps shortly."
            ];
        }

        if (normalized.Contains("wifi") || normalized.Contains("wi-fi") || normalized.Contains("internet"))
        {
            return
            [
                "Thanks for reaching out. I will send the Wi-Fi details linked to your stay right away.",
                "I can help with internet access. Are you seeing a connection error or do you need the network credentials?",
                "I have your Wi-Fi request and will provide the connection steps in a moment."
            ];
        }

        if (normalized.Contains("parking"))
        {
            return
            [
                "Thanks for your message. I will confirm the parking instructions and availability for your stay.",
                "I can help with parking details. Could you share your estimated arrival time?",
                "Understood, I am checking the parking guidance and will update you shortly."
            ];
        }

        if (normalized.Contains("late") || normalized.Contains("extend"))
        {
            return
            [
                "Thanks for asking. I will check availability for your request and confirm what is possible.",
                "I can assist with this request. Could you confirm the exact time you need so I can verify options?",
                "Understood, I am reviewing your request now and will reply with the best available option shortly."
            ];
        }

        return
        [
            "Thanks for reaching out. I have received your message and will provide a clear update shortly.",
            "Happy to help. Could you share one more detail so I can give you the most accurate response?",
            "I understand your request and I am checking the best next step for you now."
        ];
    }
}