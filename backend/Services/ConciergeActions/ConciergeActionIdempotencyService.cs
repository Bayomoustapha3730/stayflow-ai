using System.Security.Cryptography;
using System.Text;
using StayFlow.Api.Models;

namespace StayFlow.Api.Services.ConciergeActions;

public sealed class ConciergeActionIdempotencyService : IConciergeActionIdempotencyService
{
    public string CreateKey(Guid companyId, Guid conversationId, ConciergeActionType actionType, Guid propertyId, Guid? reservationId, string normalizedParameters)
    {
        var material = $"{companyId:N}|{conversationId:N}|{actionType}|{propertyId:N}|{reservationId?.ToString("N")}|{normalizedParameters}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(material));
        return Convert.ToHexString(hash);
    }
}
