using StayFlow.Api.Models;

namespace StayFlow.Api.Services;

internal static class OrganizationInvitationValidation
{
    public static string HashToken(string token, IPasswordHasher passwordHasher, IConfiguration configuration)
    {
        var pepper = configuration["Jwt:SigningKey"] ?? "stayflow-invitation-pepper";
        return passwordHasher.HashToken($"{pepper}:{token}");
    }

    public static bool TryValidate(OrganizationInvitation invitation, out string error)
    {
        if (invitation.RevokedAtUtc is not null)
        {
            error = "Invitation has been revoked.";
            return false;
        }

        if (invitation.AcceptedAtUtc is not null)
        {
            error = "Invitation has already been used.";
            return false;
        }

        if (invitation.RejectedAtUtc is not null)
        {
            error = "Invitation has already been rejected.";
            return false;
        }

        if (invitation.ExpiresAtUtc <= DateTimeOffset.UtcNow)
        {
            error = "Invitation has expired.";
            return false;
        }

        return TryValidateRole(invitation.Role, out _, out error);
    }

    public static bool TryValidateRole(string role, out string normalizedRole, out string error)
    {
        normalizedRole = string.Empty;
        error = string.Empty;

        if (!OrganizationRoleExtensions.TryParse(role, out var parsed))
        {
            error = "Invitation role is invalid.";
            return false;
        }

        if (parsed == OrganizationRole.Owner)
        {
            error = "Owner role cannot be granted through invitations.";
            return false;
        }

        normalizedRole = parsed.ToStorageValue();
        return true;
    }
}