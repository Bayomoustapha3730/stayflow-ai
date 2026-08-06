namespace StayFlow.Api.Models;

public enum OrganizationMemberStatus
{
    Active = 1,
    Suspended = 2,
    Removed = 3
}

public static class OrganizationMemberStatusExtensions
{
    public static bool TryParse(string? value, out OrganizationMemberStatus status)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            status = default;
            return false;
        }

        return Enum.TryParse(value.Trim(), ignoreCase: true, out status);
    }

    public static string ToStorageValue(this OrganizationMemberStatus status)
    {
        return status.ToString();
    }
}