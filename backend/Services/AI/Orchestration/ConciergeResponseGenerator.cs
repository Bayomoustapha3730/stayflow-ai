using StayFlow.Api.DTOs.Payments;
using StayFlow.Api.Services.AI.Intent;
using StayFlow.Api.Services.AI.Retrieval;

namespace StayFlow.Api.Services.AI.Orchestration;

public sealed class ConciergeResponseGenerator : IConciergeResponseGenerator
{
    public ConciergeResponseResult Generate(ConciergeResponseRequest request)
    {
        if (request.HumanTakeoverState)
        {
            return new ConciergeResponseResult(
                "A host is currently handling this conversation.",
                ConciergeResponseOutcome.EscalationRequired,
                true,
                false,
                [],
                request.RetrievalResult.ConfidenceLevel,
                "HumanTakeover");
        }

        if (request.IntentResult.PrimaryIntent == GuestIntent.Emergency)
        {
            var emergencyText = BuildEmergencyResponse(request);
            return new ConciergeResponseResult(
                emergencyText,
                ConciergeResponseOutcome.EscalationRequired,
                true,
                false,
                request.RetrievalResult.SelectedItems.Select(item => item.ArticleId).ToArray(),
                request.RetrievalResult.ConfidenceLevel,
                "Emergency");
        }

        if (request.IntentResult.PrimaryIntent == GuestIntent.Payment)
        {
            return BuildPaymentResponse(request);
        }

        if (request.RetrievalResult.RequiresClarification
            && ShouldAskClarification(request.IntentResult))
        {
            var normalizedQuestion = request.GuestQuestion.Trim().ToLowerInvariant();
            if (normalizedQuestion.Contains("access", StringComparison.Ordinal)
                || normalizedQuestion.Contains("enter", StringComparison.Ordinal)
                || normalizedQuestion.Contains("entry", StringComparison.Ordinal))
            {
                return new ConciergeResponseResult(
                    "Are you asking about check-in time, property entry, or Wi-Fi access?",
                    ConciergeResponseOutcome.ClarificationRequired,
                    false,
                    true,
                    [],
                    request.RetrievalResult.ConfidenceLevel,
                    "Clarification");
            }

            var choices = request.RetrievalResult.ClarificationChoices
                .Select(MapGuestFacingClarificationChoice)
                .Where(choice => !string.IsNullOrWhiteSpace(choice))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(3)
                .ToArray();

            var prompt = choices.Length == 0
                ? "Could you clarify what you need help with so I can provide the right details?"
                : choices.Length == 1
                    ? $"Could you clarify whether you are asking about {choices[0]}?"
                    : choices.Length == 2
                        ? $"Are you asking about {choices[0]} or {choices[1]}?"
                        : $"Are you asking about {choices[0]}, {choices[1]}, or {choices[2]}?";

            return new ConciergeResponseResult(
                prompt,
                ConciergeResponseOutcome.ClarificationRequired,
                false,
                true,
                [],
                request.RetrievalResult.ConfidenceLevel,
                "Clarification");
        }

        if (request.RetrievalResult.SelectedItems.Count == 0 || request.RetrievalResult.ConfidenceLevel == KnowledgeConfidenceLevel.None)
        {
            if (request.IntentResult.PrimaryIntent == GuestIntent.PetPolicy)
            {
                return new ConciergeResponseResult(
                    "I couldn't find a pet policy for this property. I can notify the host to confirm whether pets are allowed.",
                    ConciergeResponseOutcome.KnowledgeUnavailable,
                    true,
                    false,
                    [],
                    request.RetrievalResult.ConfidenceLevel,
                    "NoKnowledge");
            }

            var normalizedQuestion = request.GuestQuestion.Trim().ToLowerInvariant();
            if (normalizedQuestion.Contains("curtain", StringComparison.Ordinal)
                && normalizedQuestion.Contains("color", StringComparison.Ordinal))
            {
                return new ConciergeResponseResult(
                    "I don't have information about the curtain color. I can ask the host if you'd like.",
                    ConciergeResponseOutcome.KnowledgeUnavailable,
                    true,
                    false,
                    [],
                    request.RetrievalResult.ConfidenceLevel,
                    "NoKnowledge");
            }

            return new ConciergeResponseResult(
                "I could not find approved property information for that request. I can notify the host so they can help you directly.",
                ConciergeResponseOutcome.KnowledgeUnavailable,
                true,
                false,
                [],
                request.RetrievalResult.ConfidenceLevel,
                "NoKnowledge");
        }

        var orderedSelections = OrderSelectionsByIntent(request).Take(3).ToList();
        var lines = new List<string>();
        foreach (var selected in orderedSelections)
        {
            var sentence = BuildSentence(selected.Item.Content);
            if (!string.IsNullOrWhiteSpace(sentence))
            {
                lines.Add(sentence);
            }
        }

        if (lines.Count == 0)
        {
            return new ConciergeResponseResult(
                "I do not have enough approved details to answer this safely. I can ask the host for you.",
                ConciergeResponseOutcome.KnowledgeUnavailable,
                true,
                false,
                [],
                request.RetrievalResult.ConfidenceLevel,
                "NoGroundedFacts");
        }

        var text = string.Join(" ", lines.Distinct(StringComparer.Ordinal).Take(3));
        text = ApplyAttributeAwareAugmentation(request, text);

        return new ConciergeResponseResult(
            text,
            ConciergeResponseOutcome.Answered,
            false,
            false,
            request.RetrievalResult.SelectedItems.Select(item => item.ArticleId).Distinct(StringComparer.Ordinal).ToArray(),
            request.RetrievalResult.ConfidenceLevel,
            "Grounded");
    }

    private static string BuildEmergencyResponse(ConciergeResponseRequest request)
    {
        var grounded = request.RetrievalResult.SelectedItems
            .Select(item => BuildSentence(item.Item.Content))
            .FirstOrDefault(sentence => !string.IsNullOrWhiteSpace(sentence));

        if (!string.IsNullOrWhiteSpace(grounded))
        {
            return grounded;
        }

        return "If there is immediate danger, contact local emergency services now. I can also notify the host team immediately.";
    }

    private static ConciergeResponseResult BuildPaymentResponse(ConciergeResponseRequest request)
    {
        var grounding = request.PaymentGrounding;
        if (grounding is null)
        {
            return new ConciergeResponseResult(
                "I'm not able to verify payment details without a confirmed reservation on file. I can have the host check this for you.",
                ConciergeResponseOutcome.Answered,
                false,
                false,
                [],
                request.RetrievalResult.ConfidenceLevel,
                "PaymentGroundingUnavailable");
        }

        var normalized = request.GuestQuestion.Trim().ToLowerInvariant();
        var text = BuildPaymentAnswer(normalized, grounding);

        return new ConciergeResponseResult(
            text,
            ConciergeResponseOutcome.Answered,
            false,
            false,
            [],
            request.RetrievalResult.ConfidenceLevel,
            "PaymentGrounded");
    }

    private static string BuildPaymentAnswer(string normalizedQuestion, ReservationPaymentGroundingDto grounding)
    {
        bool AsksAbout(params string[] keywords) => keywords.Any(keyword => normalizedQuestion.Contains(keyword, StringComparison.Ordinal));

        if (IsExplicitPaymentActionRequest(normalizedQuestion))
        {
            if (grounding.RemainingBalance is <= 0m)
            {
                return "Your reservation is already fully paid. You have no remaining balance.";
            }

            if (grounding.PaymentCount == 0)
            {
                return "I don't see a completed payment recorded for this reservation yet.";
            }

            if (grounding.RemainingBalance is { } remainingBalance)
            {
                return $"There is {remainingBalance:0.00} {grounding.Currency} remaining to pay before this reservation is fully settled.";
            }

            return "I can help with the payment request, but I need the payment details to confirm the outstanding balance.";
        }

        if (AsksAbout("receipt", "transaction id", "transaction number"))
        {
            return string.IsNullOrWhiteSpace(grounding.LatestReceiptNumber)
                ? "I don't have a payment receipt on file for this reservation yet."
                : $"Your M-PESA receipt is {grounding.LatestReceiptNumber}.";
        }

        if (AsksAbout("owe", "outstanding", "balance", "remaining", "left to pay"))
        {
            return grounding.RemainingBalance is { } remaining
                ? $"You have {remaining:0.00} {grounding.Currency} remaining to pay."
                : "I don't have enough information to calculate your outstanding balance.";
        }

        if (AsksAbout("how can i pay", "how do i pay", "how to pay", "ways to pay", "payment method", "payment options"))
        {
            return "We currently accept payment via M-PESA. I can have the host follow up to arrange it, but I'm not able to start a payment request myself.";
        }

        if (AsksAbout("how much", "paid", "amount paid"))
        {
            return grounding.TotalPaid > 0
                ? $"You have paid {grounding.TotalPaid:0.00} {grounding.Currency} so far."
                : "I don't have a completed payment recorded for this reservation yet.";
        }

        return BuildPaymentStatusSummary(grounding);
    }

    private static bool IsExplicitPaymentActionRequest(string normalizedQuestion)
    {
        if (string.IsNullOrWhiteSpace(normalizedQuestion))
        {
            return false;
        }

        return normalizedQuestion.Contains("i want to pay", StringComparison.Ordinal)
            || normalizedQuestion.Contains("want to pay", StringComparison.Ordinal)
            || normalizedQuestion.Contains("i need to pay", StringComparison.Ordinal)
            || normalizedQuestion.Contains("send me an m-pesa request", StringComparison.Ordinal)
            || normalizedQuestion.Contains("send me the m-pesa prompt", StringComparison.Ordinal)
            || normalizedQuestion.Contains("send me an mpesa request", StringComparison.Ordinal)
            || normalizedQuestion.Contains("send me the mpesa prompt", StringComparison.Ordinal)
            || normalizedQuestion.Contains("send me a payment request", StringComparison.Ordinal)
            || normalizedQuestion.Contains("can i pay now", StringComparison.Ordinal)
            || normalizedQuestion.Contains("let me pay", StringComparison.Ordinal)
            || normalizedQuestion.Contains("pay now", StringComparison.Ordinal)
            || normalizedQuestion.Contains("make a payment", StringComparison.Ordinal)
            || normalizedQuestion.Contains("retry my payment", StringComparison.Ordinal)
            || normalizedQuestion.Contains("my payment failed", StringComparison.Ordinal)
            || normalizedQuestion.Contains("still owe money and want to pay", StringComparison.Ordinal);
    }

    private static string BuildPaymentStatusSummary(ReservationPaymentGroundingDto grounding)
    {
        if (grounding.HasSuccessfulPayment)
        {
            return string.IsNullOrWhiteSpace(grounding.LatestReceiptNumber)
                ? $"Yes, we received your payment of {grounding.TotalPaid:0.00} {grounding.Currency}."
                : $"Yes, we received your payment of {grounding.TotalPaid:0.00} {grounding.Currency}. Receipt: {grounding.LatestReceiptNumber}.";
        }

        if (grounding.PaymentCount == 0)
        {
            return "I don't see a completed payment recorded for this reservation yet.";
        }

        return grounding.LatestPaymentStatus switch
        {
            "Pending" or "Processing" => "Your payment is still being processed and hasn't been confirmed yet.",
            "Failed" => string.IsNullOrWhiteSpace(grounding.LatestFailureMessage)
                ? "Your last payment attempt was not completed."
                : $"Your last payment attempt was not completed: {grounding.LatestFailureMessage}",
            "Cancelled" => "Your last payment attempt was cancelled.",
            "Expired" => "Your last payment request expired before it was completed.",
            _ => "I don't have a completed payment recorded for this reservation yet."
        };
    }

    private static string BuildSentence(string content)
    {
        var normalized = string.Join(' ', content
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Trim();

        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        return normalized.Length <= 260
            ? normalized
            : normalized[..260].TrimEnd() + ".";
    }

    private static string MapGuestFacingClarificationChoice(string rawChoice)
    {
        var normalized = rawChoice.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        if (normalized.Contains("check-in", StringComparison.Ordinal)
            || normalized.Contains("checkin", StringComparison.Ordinal)
            || normalized.Contains("arrival", StringComparison.Ordinal))
        {
            return "check-in time";
        }

        if (normalized.Contains("propertyaccess", StringComparison.Ordinal)
            || normalized.Contains("access code", StringComparison.Ordinal)
            || normalized.Contains("door", StringComparison.Ordinal)
            || normalized.Contains("entry", StringComparison.Ordinal)
            || normalized.Contains("property entry", StringComparison.Ordinal))
        {
            return "property entry";
        }

        if (normalized.Contains("wifi", StringComparison.Ordinal)
            || normalized.Contains("wi-fi", StringComparison.Ordinal)
            || normalized.Contains("wireless", StringComparison.Ordinal))
        {
            return "Wi-Fi access";
        }

        if (normalized.Contains("parking", StringComparison.Ordinal))
        {
            return "parking";
        }

        if (normalized.Contains("checkout", StringComparison.Ordinal)
            || normalized.Contains("check-out", StringComparison.Ordinal))
        {
            return "checkout details";
        }

        if (normalized.Contains("pet", StringComparison.Ordinal)
            || normalized.Contains("animal", StringComparison.Ordinal))
        {
            return "pet policy";
        }

        if (normalized.Contains("house", StringComparison.Ordinal)
            || normalized.Contains("rule", StringComparison.Ordinal))
        {
            return "house rules";
        }

        if (normalized.Contains("local", StringComparison.Ordinal)
            || normalized.Contains("restaurant", StringComparison.Ordinal)
            || normalized.Contains("nearby", StringComparison.Ordinal))
        {
            return "local recommendations";
        }

        return "your request";
    }

    private static bool ShouldAskClarification(ConversationIntentResult intentResult)
    {
        if (intentResult.AllIntents().Count > 1 && !intentResult.IsAmbiguous)
        {
            return false;
        }

        return intentResult.IsAmbiguous || intentResult.Confidence < 0.70;
    }

    private static IEnumerable<KnowledgeRetrievalCandidate> OrderSelectionsByIntent(ConciergeResponseRequest request)
    {
        var selected = request.RetrievalResult.SelectedItems.ToList();
        if (selected.Count <= 1)
        {
            return selected;
        }

        var ordered = new List<KnowledgeRetrievalCandidate>();
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var intent in request.IntentResult.AllIntents())
        {
            var match = selected.FirstOrDefault(item =>
                !used.Contains(item.ArticleId)
                && IntentMatchesCategory(intent, item.Category));

            if (match is null)
            {
                continue;
            }

            ordered.Add(match);
            used.Add(match.ArticleId);
        }

        foreach (var item in selected)
        {
            if (used.Add(item.ArticleId))
            {
                ordered.Add(item);
            }
        }

        return ordered;
    }

    private static bool IntentMatchesCategory(GuestIntent intent, StayFlow.Api.Models.PropertyKnowledgeCategory category)
    {
        return intent switch
        {
            GuestIntent.WiFi => category == StayFlow.Api.Models.PropertyKnowledgeCategory.WiFi,
            GuestIntent.CheckIn or GuestIntent.PropertyAccess => category is StayFlow.Api.Models.PropertyKnowledgeCategory.CheckIn or StayFlow.Api.Models.PropertyKnowledgeCategory.Accessibility,
            GuestIntent.Checkout => category == StayFlow.Api.Models.PropertyKnowledgeCategory.Checkout,
            GuestIntent.Parking => category == StayFlow.Api.Models.PropertyKnowledgeCategory.Parking,
            GuestIntent.HouseRules or GuestIntent.PetPolicy => category == StayFlow.Api.Models.PropertyKnowledgeCategory.HouseRules,
            GuestIntent.Emergency => category == StayFlow.Api.Models.PropertyKnowledgeCategory.Emergency,
            GuestIntent.LocalRecommendations => category == StayFlow.Api.Models.PropertyKnowledgeCategory.LocalRecommendations,
            GuestIntent.Amenities => category == StayFlow.Api.Models.PropertyKnowledgeCategory.Amenities,
            _ => false
        };
    }

    private static string ApplyAttributeAwareAugmentation(ConciergeResponseRequest request, string baseText)
    {
        var question = request.GuestQuestion.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(question))
        {
            return baseText;
        }

        var selectedContent = string.Join(" ", request.RetrievalResult.SelectedItems.Select(item => item.Item.Content)).ToLowerInvariant();

        if (request.IntentResult.PrimaryIntent == GuestIntent.Parking
            && (question.Contains("free", StringComparison.Ordinal)
                || question.Contains("cost", StringComparison.Ordinal)
                || question.Contains("price", StringComparison.Ordinal)
                || question.Contains("fee", StringComparison.Ordinal)
                || question.Contains("paid", StringComparison.Ordinal))
            && !ContainsAny(selectedContent, "free", "complimentary", "cost", "price", "fee", "paid", "$"))
        {
            return $"{baseText} I do not have confirmed parking pricing details. I can ask the host to confirm whether parking is free or paid.";
        }

        if ((request.IntentResult.PrimaryIntent == GuestIntent.CheckIn || request.IntentResult.PrimaryIntent == GuestIntent.PropertyAccess)
            && (question.Contains("early", StringComparison.Ordinal)
                || question.Contains("late", StringComparison.Ordinal)
                || question.Contains("before", StringComparison.Ordinal)
                || question.Contains("after", StringComparison.Ordinal))
            && !ContainsAny(selectedContent, "early", "late", "before", "after", "flexible"))
        {
            return $"{baseText} I do not have confirmed early or late arrival policy details. I can ask the host to confirm options for your arrival time.";
        }

        return baseText;
    }

    private static bool ContainsAny(string text, params string[] tokens)
    {
        return tokens.Any(token => text.Contains(token, StringComparison.Ordinal));
    }
}
