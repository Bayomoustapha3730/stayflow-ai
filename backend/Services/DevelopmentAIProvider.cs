using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using StayFlow.Api.DTOs.AIContext;
using StayFlow.Api.DTOs.AIProvider;
using StayFlow.Api.Services.AI.Grounding;

namespace StayFlow.Api.Services;

public sealed class DevelopmentAIProvider(ILogger<DevelopmentAIProvider>? logger = null) : IAIProvider
{
    private readonly ILogger<DevelopmentAIProvider> logger = logger ?? NullLogger<DevelopmentAIProvider>.Instance;

    public Task<AIProviderResult> GenerateAsync(AIProviderRequest request, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var categories = request.QuestionCategories.Count == 0
            ? [QuestionContextCategory.General]
            : request.QuestionCategories;

        this.logger.LogInformation(
            "AI reply trace: development provider request. Categories={Categories} DetectedIntent={DetectedIntent} SelectedKnowledgeCount={SelectedKnowledgeCount} SelectedKnowledge={SelectedKnowledge} GuestMessagePreview={GuestMessagePreview}",
            categories.Select(category => category.ToString()).ToArray(),
            request.DetectedIntent,
            request.SelectedKnowledgeItems.Count,
            request.SelectedKnowledgeItems
                .Select(item => new
                {
                    item.Title,
                    item.Category,
                    ContentPreview = item.Content.Length <= 50 ? item.Content : item.Content[..50]
                })
                .ToArray(),
            request.PromptPackage.GuestMessage.Length <= 120
                ? request.PromptPackage.GuestMessage
                : request.PromptPackage.GuestMessage[..120]);

        var response = BuildResponse(categories, request);
        stopwatch.Stop();

        this.logger.LogInformation(
            "AI reply trace: development provider response. ResponsePreview={ResponsePreview}",
            response.Length <= 200 ? response : response[..200]);

        return Task.FromResult(AIProviderResult.Success(
            response,
            providerName: "Development",
            modelName: "stayflow-development-deterministic",
            requestId: Guid.NewGuid().ToString("N"),
            durationMs: stopwatch.ElapsedMilliseconds));
    }

    private static string BuildResponse(IReadOnlyCollection<QuestionContextCategory> categories, AIProviderRequest request)
    {
        if (request.ResponseConstraints.PropertyAccessRestricted || categories.Contains(QuestionContextCategory.PropertyAccess))
        {
            return "Access details require verification or host assistance. I can help contact the host.";
        }

        if (LooksLikeWiFiQuestion(request.PromptPackage.GuestMessage))
        {
            var wifiFromStructuredData = BuildWiFiResponse(request.SelectedKnowledgeItems);
            if (!string.Equals(wifiFromStructuredData, "I can see approved Wi-Fi guidance, but it does not include exact network or password values. Host verification is required before sharing details.", StringComparison.Ordinal))
            {
                return wifiFromStructuredData;
            }
        }

        if (IsWiFiIntent(categories, request.DetectedIntent))
        {
            return BuildWiFiResponse(request.SelectedKnowledgeItems);
        }

        if (TryBuildKnowledgeGroundedResponse(request, out var grounded))
        {
            return grounded;
        }

        if (categories.Contains(QuestionContextCategory.Parking))
        {
            return "Parking information is available in the approved property context. Please follow the listed parking guidance for the property.";
        }

        if (categories.Contains(QuestionContextCategory.HouseRules))
        {
            return "Please follow the approved house rules shown for your stay.";
        }

        if (categories.Contains(QuestionContextCategory.Restaurant))
        {
            return "I found nearby restaurant recommendations in the approved property context.";
        }

        if (categories.Contains(QuestionContextCategory.Emergency))
        {
            return "For emergencies, use the approved emergency contact details provided for the property. If there is immediate danger, contact local emergency services.";
        }

        if (categories.Contains(QuestionContextCategory.CheckIn))
        {
            return "Your check-in details should follow the approved reservation and property context.";
        }

        if (categories.Contains(QuestionContextCategory.CheckOut))
        {
            return "Your check-out details should follow the approved reservation and property context.";
        }

        if (categories.Contains(QuestionContextCategory.Laundry))
        {
            return "Laundry information is available in the approved property amenities or knowledge for your stay.";
        }

        return "I can help with general stay questions using the approved StayFlow context.";
    }

    private static bool TryBuildKnowledgeGroundedResponse(AIProviderRequest request, out string response)
    {
        response = string.Empty;
        if (request.SelectedKnowledgeItems.Count == 0)
        {
            return false;
        }

        var top = request.SelectedKnowledgeItems
            .Where(item => item.IsApproved)
            .OrderByDescending(item => item.Priority)
            .ThenBy(item => item.Title, StringComparer.Ordinal)
            .FirstOrDefault();

        if (top is null || string.IsNullOrWhiteSpace(top.Content))
        {
            return false;
        }

        var maxChars = Math.Clamp(request.ResponseConstraints.MaxResponseCharacters, 120, 1500);
        var concise = DeterministicGrounding.BuildConciseGuestFacingContent(top.Content, maxChars);
        if (string.IsNullOrWhiteSpace(concise))
        {
            return false;
        }

        response = concise;
        return true;
    }

    private static string BuildWiFiResponse(IReadOnlyCollection<AIProviderKnowledgeItem> items)
    {
        var approvedItems = items.Where(item => item.IsApproved).ToList();
        var extracted = DeterministicGrounding.ExtractWiFi(approvedItems);

        if (extracted.HasConflict)
        {
            return "Conflicting approved Wi-Fi information was found. Please verify the network details with the host before replying.";
        }

        var network = extracted.DistinctNetworks.Count == 1 ? extracted.DistinctNetworks.First() : null;
        var password = extracted.DistinctPasswords.Count == 1 ? extracted.DistinctPasswords.First() : null;

        if (!string.IsNullOrWhiteSpace(network) && !string.IsNullOrWhiteSpace(password))
        {
            return $"The guest Wi-Fi network is {network}, and the password is {password}.";
        }

        if (!string.IsNullOrWhiteSpace(password))
        {
            return $"The guest Wi-Fi password is {password}.";
        }

        if (!string.IsNullOrWhiteSpace(network))
        {
            return $"The guest Wi-Fi network is {network}. I\'m checking the password details.";
        }

        return "I can see approved Wi-Fi guidance, but it does not include exact network or password values. Host verification is required before sharing details.";
    }

    private static bool IsWiFiIntent(IReadOnlyCollection<QuestionContextCategory> categories, string? detectedIntent)
    {
        if (categories.Contains(QuestionContextCategory.WiFi))
        {
            return true;
        }

        return string.Equals(detectedIntent, "WiFi", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeWiFiQuestion(string guestMessage)
    {
        if (string.IsNullOrWhiteSpace(guestMessage))
        {
            return false;
        }

        var normalized = guestMessage
            .Trim()
            .ToLowerInvariant()
            .Replace("-", " ", StringComparison.Ordinal)
            .Replace("_", " ", StringComparison.Ordinal);

        return normalized.Contains("wifi", StringComparison.Ordinal)
            || normalized.Contains("wi fi", StringComparison.Ordinal)
            || normalized.Contains("network", StringComparison.Ordinal)
            || normalized.Contains("internet", StringComparison.Ordinal)
            || normalized.Contains("password", StringComparison.Ordinal);
    }
}
