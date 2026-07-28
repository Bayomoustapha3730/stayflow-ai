using StayFlow.Api.Models;
using StayFlow.Api.Services.ConciergeActions;

namespace StayFlow.Api.Tests;

public sealed class ConciergeActionIdempotencyServiceTests
{
    private readonly ConciergeActionIdempotencyService service = new();

    [Fact]
    public void CreateKey_IsTenantScoped()
    {
        var conversationId = Guid.NewGuid();
        var propertyId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();

        var first = service.CreateKey(Guid.NewGuid(), conversationId, ConciergeActionType.RequestExtraItem, propertyId, reservationId, "{\"ItemType\":0,\"Quantity\":2}");
        var second = service.CreateKey(Guid.NewGuid(), conversationId, ConciergeActionType.RequestExtraItem, propertyId, reservationId, "{\"ItemType\":0,\"Quantity\":2}");

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void CreateKey_DifferentParametersProduceDifferentKeys()
    {
        var companyId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        var propertyId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();

        var first = service.CreateKey(companyId, conversationId, ConciergeActionType.RequestExtraItem, propertyId, reservationId, "{\"ItemType\":0,\"Quantity\":2}");
        var second = service.CreateKey(companyId, conversationId, ConciergeActionType.RequestExtraItem, propertyId, reservationId, "{\"ItemType\":0,\"Quantity\":3}");

        Assert.NotEqual(first, second);
    }
}
