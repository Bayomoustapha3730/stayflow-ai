using Microsoft.Extensions.Options;

namespace StayFlow.Api.Services.Billing;

public sealed class CustomerBillingDeliveryWorkerOptions
{
    public const string SectionName = "Billing:UsageBilling:Worker";

    public bool Enabled { get; set; }
    public int PollIntervalSeconds { get; set; } = 30;
    public int BatchSize { get; set; } = 25;
    public int ReconciliationBatchSize { get; set; } = 25;
    public int RetryDelaySeconds { get; set; } = 300;
    public int ReconciliationDelaySeconds { get; set; } = 600;
    public int DispatchingRecoveryAgeSeconds { get; set; } = 900;
}

public sealed class CustomerBillingDeliveryWorkerOptionsValidator(IConfiguration configuration)
    : IValidateOptions<CustomerBillingDeliveryWorkerOptions>
{
    public ValidateOptionsResult Validate(string? name, CustomerBillingDeliveryWorkerOptions options)
    {
        if (!options.Enabled)
        {
            return ValidateOptionsResult.Success;
        }

        var errors = new List<string>();
        if (!configuration.GetValue<bool>($"{CustomerUsageBillingOptions.SectionName}:Enabled"))
        {
            errors.Add("Billing:UsageBilling:Enabled must be true when the billing delivery worker is enabled.");
        }

        if (options.PollIntervalSeconds is < 5 or > 3600)
        {
            errors.Add("Billing:UsageBilling:Worker:PollIntervalSeconds must be between 5 and 3600.");
        }

        if (options.BatchSize is < 1 or > 100)
        {
            errors.Add("Billing:UsageBilling:Worker:BatchSize must be between 1 and 100.");
        }

        if (options.ReconciliationBatchSize is < 1 or > 100)
        {
            errors.Add("Billing:UsageBilling:Worker:ReconciliationBatchSize must be between 1 and 100.");
        }

        if (options.RetryDelaySeconds is < 30 or > 86400)
        {
            errors.Add("Billing:UsageBilling:Worker:RetryDelaySeconds must be between 30 and 86400.");
        }

        if (options.ReconciliationDelaySeconds is < 300 or > 86400)
        {
            errors.Add("Billing:UsageBilling:Worker:ReconciliationDelaySeconds must be between 300 and 86400.");
        }

        if (options.DispatchingRecoveryAgeSeconds is < 60 or > 86400)
        {
            errors.Add("Billing:UsageBilling:Worker:DispatchingRecoveryAgeSeconds must be between 60 and 86400.");
        }

        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}
