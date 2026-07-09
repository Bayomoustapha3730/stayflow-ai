namespace StayFlow.Api.Services;

public interface IOpenAIResponsesClient
{
    Task<OpenAIProviderResponse> CreateResponseAsync(OpenAIProviderRequest request, CancellationToken cancellationToken);
}

public sealed class OpenAIProviderRequest
{
    public string Model { get; init; } = string.Empty;
    public int MaxOutputTokens { get; init; }
    public IReadOnlyCollection<OpenAIProviderMessage> Messages { get; init; } = [];
}

public sealed class OpenAIProviderMessage
{
    public string Role { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
}

public sealed class OpenAIProviderResponse
{
    public string? ResponseText { get; init; }
    public string? RequestId { get; init; }
    public string? ModelName { get; init; }
    public bool IsIncomplete { get; init; }
    public bool IsRefusal { get; init; }
}

public sealed class OpenAIProviderException(string failureCategory, Exception? innerException = null)
    : Exception("OpenAI provider request failed.", innerException)
{
    public string FailureCategory { get; } = failureCategory;
}
