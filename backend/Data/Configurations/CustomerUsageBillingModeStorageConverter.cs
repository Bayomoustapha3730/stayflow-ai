using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using StayFlow.Api.Models;

namespace StayFlow.Api.Data.Configurations;

public sealed class CustomerUsageBillingModeStorageConverter
    : ValueConverter<CustomerUsageBillingMode, string>
{
    public CustomerUsageBillingModeStorageConverter()
        : base(
            mode => mode == CustomerUsageBillingMode.Unsupported ? "Unsupported" : mode.ToString(),
            value => Parse(value))
    {
    }

    private static CustomerUsageBillingMode Parse(string value) =>
        Enum.TryParse<CustomerUsageBillingMode>(value, ignoreCase: false, out var mode)
        && Enum.IsDefined(mode)
            ? mode
            : CustomerUsageBillingMode.Unsupported;
}
