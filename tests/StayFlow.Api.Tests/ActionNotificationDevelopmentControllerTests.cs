using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using StayFlow.Api.Authorization;
using StayFlow.Api.Common;
using StayFlow.Api.Controllers;
using StayFlow.Api.Services.ConciergeActions;

namespace StayFlow.Api.Tests;

public sealed class ActionNotificationDevelopmentControllerTests
{
    [Fact]
    public async Task Development_InvokesProcessorExactlyOnce_AndReturnsResult()
    {
        var processor = new CountingProcessor(new ActionNotificationDeliveryResult(3, 2, 1, 0));
        var controller = CreateController(processor, "Development");

        var result = await controller.ProcessOnce(CancellationToken.None);

        Assert.Equal(1, processor.CallCount);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<ActionNotificationDeliveryResult>>(ok.Value);
        Assert.True(response.Success);
        Assert.Equal(new ActionNotificationDeliveryResult(3, 2, 1, 0), response.Data);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public async Task NonDevelopment_ReturnsNotFound_AndDoesNotInvokeProcessor(string environmentName)
    {
        var processor = new CountingProcessor(new ActionNotificationDeliveryResult(1, 1, 0, 0));
        var controller = CreateController(processor, environmentName);

        var result = await controller.ProcessOnce(CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
        Assert.Equal(0, processor.CallCount);
    }

    [Fact]
    public void Endpoint_RequiresAuthentication_AndConversationsManagePermission()
    {
        var controllerType = typeof(ActionNotificationDevelopmentController);
        var action = controllerType.GetMethod(nameof(ActionNotificationDevelopmentController.ProcessOnce))!;

        Assert.NotNull(controllerType.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true).SingleOrDefault());
        Assert.Empty(controllerType.GetCustomAttributes(typeof(AllowAnonymousAttribute), inherit: true));
        Assert.Empty(action.GetCustomAttributes(typeof(AllowAnonymousAttribute), inherit: true));

        var permission = Assert.IsType<RequiresPermissionAttribute>(
            action.GetCustomAttributes(typeof(RequiresPermissionAttribute), inherit: true).Single());
        Assert.Equal("conversations.manage", permission.Permission);

        var route = Assert.IsType<RouteAttribute>(
            controllerType.GetCustomAttributes(typeof(RouteAttribute), inherit: true).Single());
        Assert.Equal("development/action-notifications", route.Template);

        var httpPost = Assert.IsType<HttpPostAttribute>(
            action.GetCustomAttributes(typeof(HttpPostAttribute), inherit: true).Single());
        Assert.Equal("process-once", httpPost.Template);
    }

    private static ActionNotificationDevelopmentController CreateController(
        IActionNotificationDeliveryProcessor processor,
        string environmentName)
        => new(new FakeWebHostEnvironment(environmentName), processor);

    private sealed class CountingProcessor(ActionNotificationDeliveryResult result) : IActionNotificationDeliveryProcessor
    {
        public int CallCount { get; private set; }

        public Task<ActionNotificationDeliveryResult> ProcessDueAsync(CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(result);
        }
    }

    private sealed class FakeWebHostEnvironment(string environmentName) : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "StayFlow.Api.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public string WebRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
