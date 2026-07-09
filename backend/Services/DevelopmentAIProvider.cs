using System.Diagnostics;
using StayFlow.Api.DTOs.AIContext;
using StayFlow.Api.DTOs.AIProvider;

namespace StayFlow.Api.Services;

public sealed class DevelopmentAIProvider : IAIProvider
{
    public Task<AIProviderResult> GenerateAsync(AIProviderRequest request, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var categories = request.QuestionCategories.Count == 0
            ? [QuestionContextCategory.General]
            : request.QuestionCategories;
        var response = BuildResponse(categories, request);
        stopwatch.Stop();

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

        if (categories.Contains(QuestionContextCategory.WiFi))
        {
            return "WiFi details are available in the approved property information. Please use the provided network details from your stay instructions.";
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
}
