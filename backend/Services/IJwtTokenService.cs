using StayFlow.Api.DTOs.Auth;
using StayFlow.Api.Models;

namespace StayFlow.Api.Services;

public interface IJwtTokenService
{
    AuthTokenResponse CreateTokenResponse(User user, IReadOnlyCollection<string> roles, IReadOnlyCollection<string> permissions);
}
