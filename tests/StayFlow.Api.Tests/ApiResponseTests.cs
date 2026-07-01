using StayFlow.Api.Common;

namespace StayFlow.Api.Tests;

public sealed class ApiResponseTests
{
    [Fact]
    public void Ok_IncludesCorrelationId()
    {
        var response = ApiResponse<object>.Ok(new { }, "Property created successfully.", "test-correlation-id");

        Assert.True(response.Success);
        Assert.Equal("Property created successfully.", response.Message);
        Assert.NotNull(response.Data);
        Assert.Empty(response.Errors);
        Assert.Equal("test-correlation-id", response.CorrelationId);
    }

    [Fact]
    public void Fail_IncludesErrorsAndCorrelationId()
    {
        var response = ApiResponse<object>.Fail("Validation failed.", ["Name is required."], "test-correlation-id");

        Assert.False(response.Success);
        Assert.Equal("Validation failed.", response.Message);
        Assert.Null(response.Data);
        Assert.Equal("Name is required.", Assert.Single(response.Errors));
        Assert.Equal("test-correlation-id", response.CorrelationId);
    }
}
