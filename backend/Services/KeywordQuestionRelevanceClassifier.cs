using StayFlow.Api.DTOs.AIContext;

namespace StayFlow.Api.Services;

public sealed class KeywordQuestionRelevanceClassifier : IQuestionRelevanceClassifier
{
    private static readonly IReadOnlyDictionary<QuestionContextCategory, string[]> Keywords = new Dictionary<QuestionContextCategory, string[]>
    {
        [QuestionContextCategory.WiFi] = ["wifi", "wi fi", "wi-fi", "internet", "password", "network"],
        [QuestionContextCategory.CheckIn] = ["check in", "check-in", "arrival", "arrive", "early check"],
        [QuestionContextCategory.CheckOut] = ["check out", "check-out", "checkout", "leave", "departure", "late checkout"],
        [QuestionContextCategory.Parking] = ["parking", "park", "garage", "car"],
        [QuestionContextCategory.HouseRules] = ["rule", "rules", "quiet", "noise", "smoking", "party", "pet"],
        [QuestionContextCategory.Amenities] = ["amenity", "amenities", "pool", "gym", "kitchen", "air conditioning", "ac"],
        [QuestionContextCategory.Emergency] = ["emergency", "urgent", "hospital", "police", "fire", "ambulance", "doctor"],
        [QuestionContextCategory.Restaurant] = ["restaurant", "food", "eat", "dinner", "lunch", "breakfast", "cafe"],
        [QuestionContextCategory.Transportation] = ["transport", "taxi", "uber", "bolt", "driver", "airport", "transfer"],
        [QuestionContextCategory.Laundry] = ["laundry", "washer", "washing", "dryer", "iron"],
        [QuestionContextCategory.PropertyAccess] = ["door code", "lockbox", "smart lock", "gate code", "alarm code", "key", "keys", "access code", "unlock"]
    };

    public IReadOnlyCollection<QuestionContextCategory> Classify(string question)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            return [QuestionContextCategory.General];
        }

        var normalized = Normalize(question);
        var categories = Keywords
            .Where(pair => pair.Value.Any(keyword => normalized.Contains(keyword, StringComparison.Ordinal)))
            .Select(pair => pair.Key)
            .Distinct()
            .ToList();

        return categories.Count == 0 ? [QuestionContextCategory.General] : categories;
    }

    private static string Normalize(string value)
    {
        return value.Trim().ToLowerInvariant().Replace("-", " ");
    }
}
