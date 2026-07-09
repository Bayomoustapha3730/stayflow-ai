using Microsoft.Extensions.Options;

namespace StayFlow.Api.Services;

public sealed class OpenAIOptions
{
    public const string SectionName = "OpenAI";

    public string? ApiKey { get; init; }
    public string? Model { get; init; } = "gpt-5.1-mini";
    public int TimeoutSeconds { get; init; } = 30;
    public int MaxOutputTokens { get; init; } = 800;
}

public sealed class OpenAIOptionsValidator(IConfiguration configuration) : IValidateOptions<OpenAIOptions>
{
    public ValidateOptionsResult Validate(string? name, OpenAIOptions options)
    {
        var provider = configuration.GetValue<string>($"{AIProviderOptions.SectionName}:Provider") ?? "Development";
        var failures = new List<string>();

        if (options.TimeoutSeconds is < 1 or > 120)
        {
            failures.Add("OpenAI timeout must be between 1 and 120 seconds.");
        }

        if (options.MaxOutputTokens is < 1 or > 8192)
        {
            failures.Add("OpenAI max output tokens must be between 1 and 8192.");
        }

        if (provider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(options.ApiKey))
            {
                failures.Add("OpenAI API key is required when AIProvider:Provider is OpenAI.");
            }

            if (string.IsNullOrWhiteSpace(options.Model))
            {
                failures.Add("OpenAI model is required when AIProvider:Provider is OpenAI.");
            }
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
