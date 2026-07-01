namespace StayFlow.Api.DTOs.Properties;

public static class PropertyRequestValidator
{
    public static PropertyValidationResult Validate(CreatePropertyRequest request)
    {
        var result = ValidateCore(request.Name, request.AddressLine1, request.City, request.CountryCode, request.TimeZone);
        if (request.CompanyId == Guid.Empty)
        {
            result.Errors.Add("CompanyId is required.");
        }

        ValidateCollections(request.Amenities, request.HouseRules, request.LocalRecommendations, request.EmergencyContacts, request.KnowledgeBaseItems, result);
        return result;
    }

    public static PropertyValidationResult Validate(UpdatePropertyRequest request)
    {
        var result = ValidateCore(request.Name, request.AddressLine1, request.City, request.CountryCode, request.TimeZone);
        ValidateCollections(request.Amenities, request.HouseRules, request.LocalRecommendations, request.EmergencyContacts, request.KnowledgeBaseItems, result);
        return result;
    }

    private static PropertyValidationResult ValidateCore(string name, string addressLine1, string city, string countryCode, string timeZone)
    {
        var result = new PropertyValidationResult();

        AddRequired(result, name, "Property name", 180);
        AddRequired(result, addressLine1, "Address line 1", 240);
        AddRequired(result, city, "City", 120);

        if (countryCode.Length != 2)
        {
            result.Errors.Add("Country code must be a two-letter ISO code.");
        }

        AddRequired(result, timeZone, "Time zone", 80);
        return result;
    }

    private static void ValidateCollections(
        IReadOnlyCollection<AmenityRequest> amenities,
        IReadOnlyCollection<HouseRuleRequest> houseRules,
        IReadOnlyCollection<LocalRecommendationRequest> recommendations,
        IReadOnlyCollection<EmergencyContactRequest> contacts,
        IReadOnlyCollection<PropertyKnowledgeBaseItemRequest> knowledgeBaseItems,
        PropertyValidationResult result)
    {
        foreach (var amenity in amenities)
        {
            AddRequired(result, amenity.Name, "Amenity name", 120);
        }

        foreach (var rule in houseRules)
        {
            AddRequired(result, rule.Title, "House rule title", 160);
            AddRequired(result, rule.Description, "House rule description", 1000);
        }

        foreach (var recommendation in recommendations)
        {
            AddRequired(result, recommendation.Name, "Local recommendation name", 160);
            AddRequired(result, recommendation.Category, "Local recommendation category", 100);
        }

        foreach (var contact in contacts)
        {
            AddRequired(result, contact.Name, "Emergency contact name", 160);
            AddRequired(result, contact.Role, "Emergency contact role", 100);
            AddRequired(result, contact.PhoneNumber, "Emergency contact phone number", 32);
        }

        foreach (var item in knowledgeBaseItems)
        {
            AddRequired(result, item.Title, "Knowledge base title", 200);
            AddRequired(result, item.Content, "Knowledge base content", 5000);
        }
    }

    private static void AddRequired(PropertyValidationResult result, string? value, string fieldName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            result.Errors.Add($"{fieldName} is required.");
            return;
        }

        if (value.Length > maxLength)
        {
            result.Errors.Add($"{fieldName} must be {maxLength} characters or fewer.");
        }
    }
}
