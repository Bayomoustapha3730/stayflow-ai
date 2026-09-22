using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using StayFlow.Api.Controllers;
using StayFlow.Api.DTOs.AIOrchestration;
using StayFlow.Api.Services;

namespace StayFlow.Api.Tests;

public sealed class AIOrchestrationControllerTests
{
    [Fact]
    public async Task LegacyOrchestration_Returns410WithoutInvokingOrchestrator()
    {
        var orchestrator = new RecordingOrchestrator();
        var controller = new AIOrchestrationController(orchestrator);

        var result = await controller.Orchestrate(new AIOrchestrationRequest
        {
            GuestMessage = "Hello"
        }, CancellationToken.None);

        var status = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status410Gone, status.StatusCode);
        Assert.False(orchestrator.Invoked);
    }

    private sealed class RecordingOrchestrator : IAIOrchestrator
    {
        public bool Invoked { get; private set; }

        public Task<AIOrchestrationResult> ProcessAsync(AIOrchestrationRequest request, CancellationToken cancellationToken)
        {
            Invoked = true;
            return Task.FromResult(new AIOrchestrationResult());
        }
    }
}
