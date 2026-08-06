namespace StayFlow.Api.Models;

public enum OrganizationRole
{
    ReadOnly = 10,
    Support = 20,
    Host = 30,
    Manager = 40,
    Administrator = 50,
    Owner = 60
}

public static class OrganizationRoleExtensions
{
    public static bool TryParse(string? value, out OrganizationRole role)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            role = default;
            return false;
        }

        return Enum.TryParse(value.Trim(), ignoreCase: true, out role);
    }

    public static string ToStorageValue(this OrganizationRole role)
    {
        return role.ToString();
    }
}