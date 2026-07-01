namespace StayFlow.Api.Services;

public interface IPasswordHasher
{
    string HashPassword(string password);
    bool VerifyPassword(string password, string passwordHash);
    string HashToken(string token);
    string GenerateSecureToken();
}
