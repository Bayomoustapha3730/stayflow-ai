using StayFlow.Api.Common;
using StayFlow.Api.DTOs.AIOrchestration;
using StayFlow.Api.DTOs.Chat;
using StayFlow.Api.Models;
using StayFlow.Api.Repositories;
using StayFlow.Api.Services;

namespace StayFlow.Api.Tests;

public sealed class ChatServiceTests
{
    [Fact]
    public async Task SendMessageAsync_WithNewTenantScopedConversation_PersistsGuestAndAssistantMessages()
    {
        var fixture = new Fixture();

        var response = await fixture.Service.SendMessageAsync(new SendChatMessageRequest
        {
            GuestId = fixture.GuestId,
            PropertyId = fixture.PropertyId,
            Message = "What are my check-in and check-out dates?",
            Channel = "Web"
        }, CancellationToken.None);

        Assert.True(response.Success);
        Assert.NotEqual(Guid.Empty, response.Data!.ConversationId);
        Assert.Equal(AIOrchestrationOutcome.Responded, response.Data.Outcome);
        Assert.Equal(2, fixture.Repository.Messages.Count);
        Assert.Equal("Guest", response.Data.GuestMessage.SenderType);
        Assert.Equal("Assistant", response.Data.AssistantMessage.SenderType);
        Assert.Equal(response.Data.ConversationId, fixture.Orchestrator.LastRequest!.ConversationId);
        Assert.Equal(fixture.GuestId, fixture.Orchestrator.LastRequest.GuestId);
    }

    [Fact]
    public async Task GetHistoryAsync_ReturnsTenantScopedMessages()
    {
        var fixture = new Fixture();
        var conversation = fixture.Repository.NewConversation();
        fixture.Repository.Conversations.Add(conversation);
        fixture.Repository.Messages.Add(fixture.Repository.NewMessage(conversation.Id, "Guest", "Hello"));
        fixture.Repository.Messages.Add(fixture.Repository.NewMessage(conversation.Id, "Assistant", "Hi"));

        var response = await fixture.Service.GetHistoryAsync(new ChatHistoryQueryParameters
        {
            ConversationId = conversation.Id
        }, CancellationToken.None);

        Assert.True(response.Success);
        Assert.Equal(2, response.Data!.TotalCount);
        Assert.Equal(["Guest", "Assistant"], response.Data.Items.Select(message => message.SenderType));
    }

    [Fact]
    public async Task EscalateAsync_UpdatesConversationStatusAndWritesInternalMessage()
    {
        var fixture = new Fixture();
        var conversation = fixture.Repository.NewConversation();
        fixture.Repository.Conversations.Add(conversation);

        var response = await fixture.Service.EscalateAsync(new EscalateChatRequest
        {
            ConversationId = conversation.Id,
            Reason = "Needs host approval"
        }, CancellationToken.None);

        Assert.True(response.Success);
        Assert.Equal("Escalated", response.Data!.Status);
        Assert.Contains(fixture.Repository.Messages, message => message.IsInternal && message.Body.Contains("Needs host approval"));
    }

    [Fact]
    public async Task EndAsync_UpdatesConversationStatusAndWritesInternalMessage()
    {
        var fixture = new Fixture();
        var conversation = fixture.Repository.NewConversation();
        fixture.Repository.Conversations.Add(conversation);

        var response = await fixture.Service.EndAsync(new EndChatRequest
        {
            ConversationId = conversation.Id
        }, CancellationToken.None);

        Assert.True(response.Success);
        Assert.Equal("Ended", response.Data!.Status);
        Assert.Contains(fixture.Repository.Messages, message => message.IsInternal && message.Body == "Conversation ended.");
    }

    [Fact]
    public async Task SendMessageAsync_WithoutConversationOrGuestProperty_ReturnsValidationFailure()
    {
        var fixture = new Fixture();

        var response = await fixture.Service.SendMessageAsync(new SendChatMessageRequest
        {
            Message = "Hello"
        }, CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal("Chat message validation failed.", response.Message);
        Assert.Contains("GuestId and PropertyId are required when ConversationId is not supplied.", response.Errors);
    }

    [Fact]
    public async Task SendMessageAsync_WithMissingTenantContext_FailsSafely()
    {
        var fixture = new Fixture(new FakeCurrentTenantContext(null));

        var response = await fixture.Service.SendMessageAsync(new SendChatMessageRequest
        {
            GuestId = fixture.GuestId,
            PropertyId = fixture.PropertyId,
            Message = "Hello"
        }, CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal("Authenticated tenant context is missing or invalid.", response.Message);
        Assert.Empty(fixture.Repository.Messages);
    }

    [Fact]
    public async Task GetHistoryAsync_WithCrossTenantConversation_ReturnsNotFound()
    {
        var fixture = new Fixture();
        fixture.Repository.Conversations.Add(fixture.Repository.NewConversation(overrideCompanyId: Guid.NewGuid()));

        var response = await fixture.Service.GetHistoryAsync(new ChatHistoryQueryParameters
        {
            ConversationId = fixture.Repository.Conversations.Single().Id
        }, CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal("Conversation was not found.", response.Message);
    }

    private sealed class Fixture
    {
        public Fixture(FakeCurrentTenantContext? tenantContext = null)
        {
            TenantContext = tenantContext ?? new FakeCurrentTenantContext(CompanyId);
            Repository = new FakeChatRepository(CompanyId, GuestId, PropertyId);
            Orchestrator = new FakeAIOrchestrator();
            Service = new ChatService(Repository, Orchestrator, TenantContext);
        }

        public Guid CompanyId { get; } = Guid.NewGuid();
        public Guid GuestId { get; } = Guid.NewGuid();
        public Guid PropertyId { get; } = Guid.NewGuid();
        public FakeCurrentTenantContext TenantContext { get; }
        public FakeChatRepository Repository { get; }
        public FakeAIOrchestrator Orchestrator { get; }
        public ChatService Service { get; }
    }

    private sealed class FakeCurrentTenantContext(Guid? companyId, bool isAuthenticated = true) : ICurrentTenantContext
    {
        public Guid? CompanyId { get; } = companyId;
        public Guid? UserId { get; } = Guid.NewGuid();
        public string? CorrelationId { get; } = "chat-test";
        public bool IsAuthenticated { get; } = isAuthenticated;
    }

    private sealed class FakeAIOrchestrator : IAIOrchestrator
    {
        public AIOrchestrationRequest? LastRequest { get; private set; }

        public Task<AIOrchestrationResult> ProcessAsync(AIOrchestrationRequest request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new AIOrchestrationResult
            {
                Outcome = AIOrchestrationOutcome.Responded,
                GuestSafeMessage = "Your check-in date is 2026-08-01 and check-out date is 2026-08-04."
            });
        }
    }

    private sealed class FakeChatRepository(Guid companyId, Guid guestId, Guid propertyId) : IChatRepository
    {
        public List<Conversation> Conversations { get; } = [];
        public List<ConversationMessage> Messages { get; } = [];

        public Task<Conversation?> GetConversationAsync(Guid requestedCompanyId, Guid conversationId, CancellationToken cancellationToken)
        {
            return Task.FromResult(Conversations.FirstOrDefault(conversation => conversation.CompanyId == requestedCompanyId && conversation.Id == conversationId));
        }

        public Task<bool> GuestBelongsToCompanyAsync(Guid requestedCompanyId, Guid requestedGuestId, CancellationToken cancellationToken)
        {
            return Task.FromResult(requestedCompanyId == companyId && requestedGuestId == guestId);
        }

        public Task<bool> PropertyBelongsToCompanyAsync(Guid requestedCompanyId, Guid requestedPropertyId, CancellationToken cancellationToken)
        {
            return Task.FromResult(requestedCompanyId == companyId && requestedPropertyId == propertyId);
        }

        public Task AddConversationAsync(Conversation conversation, CancellationToken cancellationToken)
        {
            Conversations.Add(conversation);
            return Task.CompletedTask;
        }

        public Task AddMessageAsync(ConversationMessage message, CancellationToken cancellationToken)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }

        public Task<PagedResult<ConversationMessage>> GetMessagesAsync(Guid requestedCompanyId, ChatHistoryQueryParameters query, CancellationToken cancellationToken)
        {
            var pageNumber = query.NormalizedPageNumber;
            var pageSize = query.NormalizedPageSize;
            var scopedMessages = Messages
                .Where(message => message.CompanyId == requestedCompanyId && message.ConversationId == query.ConversationId)
                .OrderBy(message => message.CreatedAt)
                .ToList();

            return Task.FromResult(new PagedResult<ConversationMessage>
            {
                Items = scopedMessages.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList(),
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalCount = scopedMessages.Count
            });
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            var now = DateTimeOffset.UtcNow;
            foreach (var conversation in Conversations.Where(conversation => conversation.CreatedAt == default))
            {
                conversation.CreatedAt = now;
                conversation.UpdatedAt = now;
            }

            foreach (var message in Messages.Where(message => message.CreatedAt == default))
            {
                message.CreatedAt = now;
                message.UpdatedAt = now;
            }

            return Task.CompletedTask;
        }

        public Conversation NewConversation(Guid? overrideCompanyId = null)
        {
            return new Conversation
            {
                Id = Guid.NewGuid(),
                CompanyId = overrideCompanyId ?? companyId,
                GuestId = guestId,
                PropertyId = propertyId,
                Channel = "Web",
                Status = "Open",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
        }

        public ConversationMessage NewMessage(Guid conversationId, string senderType, string body)
        {
            return new ConversationMessage
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                ConversationId = conversationId,
                SenderType = senderType,
                Body = body,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
        }
    }
}
