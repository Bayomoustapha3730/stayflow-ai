using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StayFlow.Api.DTOs.AIPrompt;
using StayFlow.Api.DTOs.AIProvider;
using StayFlow.Api.Services;

namespace StayFlow.Api.Tests;

public sealed class OpenAIAIProviderTests
{
    [Fact]
    public async Task GenerateAsync_MapsRenderedMessagesInOrder()
    {
        var fixture = new Fixture();
        var request = ProviderRequest([
            new AIPromptMessage { Role = "system", Content = "system instructions" },
            new AIPromptMessage { Role = "developer", Content = "developer context" },
            new AIPromptMessage { Role = "user", Content = "guest question" }
        ]);

        await fixture.Provider.GenerateAsync(request, CancellationToken.None);

        Assert.Collection(
            fixture.Client.LastRequest!.Messages,
            message =>
            {
                Assert.Equal("system", message.Role);
                Assert.Equal("system instructions", message.Content);
            },
            message =>
            {
                Assert.Equal("developer", message.Role);
                Assert.Equal("developer context", message.Content);
            },
            message =>
            {
                Assert.Equal("user", message.Role);
                Assert.Equal("guest question", message.Content);
            });
    }

    [Fact]
    public async Task GenerateAsync_KeepsGuestInputAsUserMessageWithoutRewriting()
    {
        var fixture = new Fixture();
        var guestInput = $"guest typed arbitrary guid {Guid.NewGuid()}";

        await fixture.Provider.GenerateAsync(ProviderRequest([
            new AIPromptMessage { Role = "user", Content = guestInput }
        ]), CancellationToken.None);

        var message = Assert.Single(fixture.Client.LastRequest!.Messages);
        Assert.Equal("user", message.Role);
        Assert.Equal(guestInput, message.Content);
    }

    [Fact]
    public async Task GenerateAsync_DoesNotAddCompanyOrInternalIdentifiers()
    {
        var fixture = new Fixture();
        var companyId = Guid.NewGuid();

        await fixture.Provider.GenerateAsync(ProviderRequest([
            new AIPromptMessage { Role = "developer", Content = "approved context only" },
            new AIPromptMessage { Role = "user", Content = "What is check-in?" }
        ], companyId), CancellationToken.None);

        var sentText = string.Join(Environment.NewLine, fixture.Client.LastRequest!.Messages.Select(message => message.Content));
        Assert.DoesNotContain(companyId.ToString(), sentText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GenerateAsync_UsesConfiguredModelAndMaxOutputTokens()
    {
        var fixture = new Fixture(model: "gpt-test", maxOutputTokens: 321);

        await fixture.Provider.GenerateAsync(ProviderRequest(), CancellationToken.None);

        Assert.Equal("gpt-test", fixture.Client.LastRequest!.Model);
        Assert.Equal(321, fixture.Client.LastRequest.MaxOutputTokens);
    }

    [Fact]
    public async Task GenerateAsync_MapsSuccessfulResponse()
    {
        var fixture = new Fixture();
        fixture.Client.Response = new OpenAIProviderResponse
        {
            ResponseText = "Safe response",
            RequestId = "resp_123",
            ModelName = "gpt-test"
        };

        var result = await fixture.Provider.GenerateAsync(ProviderRequest(), CancellationToken.None);

        Assert.Equal(AIProviderOutcome.Success, result.Outcome);
        Assert.Equal("OpenAI", result.ProviderName);
        Assert.Equal("Safe response", result.ResponseText);
        Assert.Equal("resp_123", result.RequestId);
        Assert.Equal("gpt-test", result.ModelName);
    }

    [Theory]
    [InlineData(OpenAIProviderFailureCategories.RateLimited, AIProviderOutcome.Unavailable)]
    [InlineData(OpenAIProviderFailureCategories.Authentication, AIProviderOutcome.Failed)]
    [InlineData(OpenAIProviderFailureCategories.InvalidRequest, AIProviderOutcome.Failed)]
    [InlineData(OpenAIProviderFailureCategories.ProviderServerError, AIProviderOutcome.Unavailable)]
    [InlineData(OpenAIProviderFailureCategories.Network, AIProviderOutcome.Unavailable)]
    public async Task GenerateAsync_MapsProviderFailureCategories(string failureCategory, AIProviderOutcome expectedOutcome)
    {
        var fixture = new Fixture();
        fixture.Client.Exception = new OpenAIProviderException(failureCategory);

        var result = await fixture.Provider.GenerateAsync(ProviderRequest(), CancellationToken.None);

        Assert.Equal(expectedOutcome, result.Outcome);
        Assert.Equal(failureCategory, result.FailureCategory);
    }

    [Fact]
    public async Task GenerateAsync_EmptyResponseFailsSafely()
    {
        var fixture = new Fixture();
        fixture.Client.Response = new OpenAIProviderResponse { ResponseText = " " };

        var result = await fixture.Provider.GenerateAsync(ProviderRequest(), CancellationToken.None);

        Assert.Equal(AIProviderOutcome.Failed, result.Outcome);
        Assert.Equal(OpenAIProviderFailureCategories.EmptyResponse, result.FailureCategory);
    }

    [Fact]
    public async Task GenerateAsync_CancellationReturnsCancelledFailure()
    {
        var fixture = new Fixture();
        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        var result = await fixture.Provider.GenerateAsync(ProviderRequest(), cancellationTokenSource.Token);

        Assert.Equal(AIProviderOutcome.Unavailable, result.Outcome);
        Assert.Equal(OpenAIProviderFailureCategories.Cancelled, result.FailureCategory);
    }

    [Fact]
    public async Task GenerateAsync_LogsOnlySanitizedMetadata()
    {
        var apiKey = "sk-test-secret-value";
        var guestPrompt = "full guest prompt secret";
        var providerResponse = "full provider response secret";
        var fixture = new Fixture(apiKey: apiKey);
        fixture.Client.Response = new OpenAIProviderResponse { ResponseText = providerResponse };

        await fixture.Provider.GenerateAsync(ProviderRequest([
            new AIPromptMessage { Role = "user", Content = guestPrompt }
        ]), CancellationToken.None);

        var logs = string.Join(Environment.NewLine, fixture.Logger.Messages);
        Assert.DoesNotContain(apiKey, logs, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(guestPrompt, logs, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(providerResponse, logs, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void OpenAIOptionsValidator_AllowsDevelopmentWithoutApiKey()
    {
        var validator = new OpenAIOptionsValidator(Configuration(("AIProvider:Provider", "Development")));

        var result = validator.Validate(null, new OpenAIOptions { ApiKey = null, Model = "gpt-test", TimeoutSeconds = 30, MaxOutputTokens = 800 });

        Assert.False(result.Failed);
    }

    [Fact]
    public void OpenAIOptionsValidator_RequiresApiKeyAndModelForOpenAIProvider()
    {
        var validator = new OpenAIOptionsValidator(Configuration(("AIProvider:Provider", "OpenAI")));

        var result = validator.Validate(null, new OpenAIOptions { ApiKey = null, Model = "", TimeoutSeconds = 30, MaxOutputTokens = 800 });

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, failure => failure.Contains("API key", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Failures!, failure => failure.Contains("model", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(0, 800)]
    [InlineData(121, 800)]
    [InlineData(30, 0)]
    [InlineData(30, 8193)]
    public void OpenAIOptionsValidator_RejectsInvalidTimeoutOrTokenLimits(int timeoutSeconds, int maxOutputTokens)
    {
        var validator = new OpenAIOptionsValidator(Configuration(("AIProvider:Provider", "Development")));

        var result = validator.Validate(null, new OpenAIOptions
        {
            ApiKey = null,
            Model = "gpt-test",
            TimeoutSeconds = timeoutSeconds,
            MaxOutputTokens = maxOutputTokens
        });

        Assert.True(result.Failed);
    }

    private static AIProviderRequest ProviderRequest(IReadOnlyCollection<AIPromptMessage>? messages = null, Guid? companyId = null)
    {
        var contextText = companyId.HasValue ? "tenant context was resolved internally" : "approved context";
        return new AIProviderRequest
        {
            PromptPackage = new AIPromptPackage
            {
                ContextSections =
                [
                    new AIPromptContextSection { Title = "Context", Items = [contextText] }
                ]
            },
            RenderedMessages = messages ?? [new AIPromptMessage { Role = "user", Content = "Hello" }],
            CorrelationId = "provider-test"
        };
    }

    private static IConfiguration Configuration(params (string Key, string Value)[] values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(value => new KeyValuePair<string, string?>(value.Key, value.Value)))
            .Build();
    }

    private sealed class Fixture
    {
        public Fixture(string apiKey = "sk-test", string model = "gpt-test", int maxOutputTokens = 800)
        {
            Client = new FakeOpenAIResponsesClient();
            Logger = new CapturingLogger<OpenAIAIProvider>();
            Provider = new OpenAIAIProvider(
                Client,
                Options.Create(new OpenAIOptions
                {
                    ApiKey = apiKey,
                    Model = model,
                    TimeoutSeconds = 30,
                    MaxOutputTokens = maxOutputTokens
                }),
                Logger);
        }

        public FakeOpenAIResponsesClient Client { get; }
        public CapturingLogger<OpenAIAIProvider> Logger { get; }
        public OpenAIAIProvider Provider { get; }
    }

    private sealed class FakeOpenAIResponsesClient : IOpenAIResponsesClient
    {
        public OpenAIProviderRequest? LastRequest { get; private set; }
        public OpenAIProviderResponse Response { get; set; } = new() { ResponseText = "Safe response", RequestId = "resp_test", ModelName = "gpt-test" };
        public Exception? Exception { get; set; }

        public Task<OpenAIProviderResponse> CreateResponseAsync(OpenAIProviderRequest request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            cancellationToken.ThrowIfCancellationRequested();
            if (Exception is not null)
            {
                throw Exception;
            }

            return Task.FromResult(Response);
        }
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }
    }
}
