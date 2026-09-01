using System.Security.Cryptography;
using System.Text;
using StayFlow.Api.Models;

namespace StayFlow.Api.Services;

public sealed class ReservationLifecycleEventIdempotencyKeyBuilder : IReservationLifecycleEventIdempotencyKeyBuilder
{
    public string Build(Guid companyId, Guid reservationId, ReservationLifecycleEventType eventType, DateOnly propertyLocalDate, string ruleVersion)
    {
        var material = $"{companyId:N}|{reservationId:N}|{eventType}|{propertyLocalDate:yyyy-MM-dd}|{ruleVersion}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(material));
        return Convert.ToHexString(hash);
    }
}
