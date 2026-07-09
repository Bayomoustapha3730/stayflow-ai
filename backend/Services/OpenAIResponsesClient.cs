using OpenAI.Responses;
using System.ClientModel;
using Microsoft.Extensions.Options;

namespace StayFlow.Api.Services;

#pragma warning disable OPENAI001
public sealed class OpenAIResponsesClient : IOpenAIResponsesClient
{
    private readonly ResponsesClient responsesClient;

    public OpenAIResponsesClient(IOptions<OpenAIOptions> options)
    {
        var apiKey = options.Value.ApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("OpenAI API key is required to create the OpenAI responses client.");
        }

        responsesClient = new ResponsesClient(apiKey);
    }

    public async Task<OpenAIProviderResponse> CreateResponseAsync(OpenAIProviderRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var options = new CreateResponseOptions
            {
                Model = request.Model,
                MaxOutputTokenCount = request.MaxOutputTokens
            };

            foreach (var message in request.Messages)
            {
                options.InputItems.Add(MapMessage(message));
            }

            ResponseResult response = await responsesClient.CreateResponseAsync(options, cancellationToken);
            var status = response.Status?.ToString() ?? string.Empty;

            return new OpenAIProviderResponse
            {
                ResponseText = response.GetOutputText(),
                RequestId = response.Id,
                ModelName = request.Model,
                IsIncomplete = status.Contains("Incomplete", StringComparison.OrdinalIgnoreCase),
                IsRefusal = status.Contains("Refusal", StringComparison.OrdinalIgnoreCase)
            };
        }
        catch (ClientResultException exception)
        {
            throw new OpenAIProviderException(MapStatusCode(exception.Status), exception);
        }
        catch (HttpRequestException exception)
        {
            throw new OpenAIProviderException(OpenAIProviderFailureCategories.Network, exception);
        }
    }

    private static ResponseItem MapMessage(OpenAIProviderMessage message)
    {
        return message.Role switch
        {
            "system" => ResponseItem.CreateSystemMessageItem(message.Content),
            "developer" => ResponseItem.CreateDeveloperMessageItem(message.Content),
            "assistant" => ResponseItem.CreateAssistantMessageItem(message.Content),
            _ => ResponseItem.CreateUserMessageItem(message.Content)
        };
    }

    private static string MapStatusCode(int statusCode)
    {
        return statusCode switch
        {
            401 or 403 => OpenAIProviderFailureCategories.Authentication,
            400 or 404 or 422 => OpenAIProviderFailureCategories.InvalidRequest,
            429 => OpenAIProviderFailureCategories.RateLimited,
            >= 500 => OpenAIProviderFailureCategories.ProviderServerError,
            _ => OpenAIProviderFailureCategories.Unknown
        };
    }
}
#pragma warning restore OPENAI001
