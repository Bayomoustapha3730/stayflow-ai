namespace StayFlow.Api.DTOs.Properties;

public static class PropertyRequestValidator
{
    public static PropertyValidationResult Validate(CreatePropertyRequest request)
    {
        var result = ValidateCore(request.Name, request.AddressLine1, request.City, request.CountryCode, request.TimeZone);

        ValidateCollections(request.PropertyAmenities, request.PropertyHouseRules, request.PropertyRecommendations, request.PropertyEmergencyContacts, request.PropertyKnowledgeArticles, result);
        return result;
    }

    public static PropertyValidationResult Validate(UpdatePropertyRequest request)
    {
        var result = ValidateCore(request.Name, request.AddressLine1, request.City, request.CountryCode, request.TimeZone);

        ValidateCollections(request.PropertyAmenities, request.PropertyHouseRules, request.PropertyRecommendations, request.PropertyEmergencyContacts, request.PropertyKnowledgeArticles, result);
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
        IReadOnlyCollection<PropertyAmenityRequest> propertyAmenities,
        IReadOnlyCollection<PropertyHouseRuleRequest> propertyHouseRules,
        IReadOnlyCollection<PropertyRecommendationRequest> recommendations,
        IReadOnlyCollection<PropertyEmergencyContactRequest> contacts,
        IReadOnlyCollection<PropertyKnowledgeArticleRequest> propertyKnowledgeArticles,
        PropertyValidationResult result)
    {
        foreach (var amenity in propertyAmenities)
        {
            AddRequired(result, amenity.Name, "Amenity name", 120);
        }

        foreach (var rule in propertyHouseRules)
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

        foreach (var item in propertyKnowledgeArticles)
        {
            AddRequired(result, item.Title, "Knowledge article title", 200);
            AddRequired(result, item.Content, "Knowledge article content", 5000);
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
