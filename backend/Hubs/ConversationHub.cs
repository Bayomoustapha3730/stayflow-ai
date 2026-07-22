using System.Collections.Concurrent;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using StayFlow.Api.Repositories;

namespace StayFlow.Api.Hubs;

[Authorize]
public sealed class ConversationHub(
    IConversationRepository conversationRepository,
    Services.ICurrentTenantContext currentTenantContext,
    Services.IConversationRealtimePublisher realtimePublisher,
    ILogger<ConversationHub> logger) : Hub
{
    private static readonly TimeSpan TypingTtl = TimeSpan.FromSeconds(5);
    private static readonly ConcurrentDictionary<string, CancellationTokenSource> ActiveTyping = new();

    public async Task JoinConversation(Guid conversationId)
    {
        if (!TryGetTenantContext(out var companyId))
        {
            throw new HubException("Authenticated tenant context is required.");
        }

        var conversation = await conversationRepository.GetByIdForCompanyAsync(companyId, conversationId, Context.ConnectionAborted);
        if (conversation is null)
        {
            throw new HubException("Conversation was not found.");
        }

        if (!HasAnyPermission("chat.read", "conversations.read"))
        {
            throw new HubException("Permission denied.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, ConversationHubChannels.ConversationGroup(conversationId));

        if (HasAnyPermission("conversations.read", "conversations.reply", "conversations.notes"))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, ConversationHubChannels.HostCompanyGroup(companyId));
        }
    }

    public Task LeaveConversation(Guid conversationId)
    {
        return Groups.RemoveFromGroupAsync(Context.ConnectionId, ConversationHubChannels.ConversationGroup(conversationId));
    }

    public async Task StartTyping(Guid conversationId, string context)
    {
        var normalizedContext = NormalizeContext(context);
        if (normalizedContext is null)
        {
            throw new HubException("Unsupported typing context.");
        }

        if (!TryGetTenantContext(out var companyId))
        {
            throw new HubException("Authenticated tenant context is required.");
        }

        var conversation = await conversationRepository.GetByIdForCompanyAsync(companyId, conversationId, Context.ConnectionAborted);
        if (conversation is null)
        {
            throw new HubException("Conversation was not found.");
        }

        EnsureTypingPermission(normalizedContext);

        var actorId = currentTenantContext.UserId?.ToString("D") ?? string.Empty;
        var actorName = Context.User?.FindFirstValue(ClaimTypes.Name) ?? string.Empty;
        var key = $"{companyId:D}:{conversationId:D}:{normalizedContext}:{actorId}";

        if (ActiveTyping.TryGetValue(key, out var existing))
        {
            existing.Cancel();
            existing.Dispose();
        }

        var cts = new CancellationTokenSource();
        ActiveTyping[key] = cts;

        var payload = new
        {
            conversationId,
            context = normalizedContext,
            actorUserId = actorId,
            actorName,
            timestamp = DateTimeOffset.UtcNow
        };

        if (existing is null)
        {
            await realtimePublisher.PublishTypingStartedAsync(companyId, conversationId, payload, normalizedContext == "internal-note" || normalizedContext == "guest", Context.ConnectionAborted);
        }

        _ = ExpireTypingAsync(companyId, conversationId, normalizedContext, actorId, actorName, key, cts);
    }

    public async Task StopTyping(Guid conversationId, string context)
    {
        var normalizedContext = NormalizeContext(context);
        if (normalizedContext is null)
        {
            return;
        }

        if (!TryGetTenantContext(out var companyId))
        {
            return;
        }

        var actorId = currentTenantContext.UserId?.ToString("D") ?? string.Empty;
        var actorName = Context.User?.FindFirstValue(ClaimTypes.Name) ?? string.Empty;
        var key = $"{companyId:D}:{conversationId:D}:{normalizedContext}:{actorId}";

        if (!ActiveTyping.TryRemove(key, out var existing))
        {
            return;
        }

        existing.Cancel();
        existing.Dispose();

        await realtimePublisher.PublishTypingStoppedAsync(companyId, conversationId, new
        {
            conversationId,
            context = normalizedContext,
            actorUserId = actorId,
            actorName,
            timestamp = DateTimeOffset.UtcNow
        }, normalizedContext == "internal-note" || normalizedContext == "guest", Context.ConnectionAborted);
    }

    private async Task ExpireTypingAsync(
        Guid companyId,
        Guid conversationId,
        string context,
        string actorId,
        string actorName,
        string key,
        CancellationTokenSource cts)
    {
        try
        {
            await Task.Delay(TypingTtl, cts.Token);
            if (!ActiveTyping.TryRemove(key, out _))
            {
                return;
            }

            await realtimePublisher.PublishTypingStoppedAsync(companyId, conversationId, new
            {
                conversationId,
                context,
                actorUserId = actorId,
                actorName,
                timestamp = DateTimeOffset.UtcNow
            }, context == "internal-note" || context == "guest", CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            // expected when typing is refreshed or explicitly stopped
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed expiring typing indicator for conversation {ConversationId}", conversationId);
        }
    }

    private void EnsureTypingPermission(string context)
    {
        if (context == "guest")
        {
            if (!HasAnyPermission("chat.send"))
            {
                throw new HubException("Permission denied.");
            }

            return;
        }

        if (context == "host" && !HasAnyPermission("conversations.reply"))
        {
            throw new HubException("Permission denied.");
        }

        if (context == "internal-note" && !HasAnyPermission("conversations.notes"))
        {
            throw new HubException("Permission denied.");
        }
    }

    private bool HasAnyPermission(params string[] permissions)
    {
        var claims = Context.User?.FindAll("permission").Select(claim => claim.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return claims is not null && permissions.Any(claims.Contains);
    }

    private static string? NormalizeContext(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();
        return normalized is "guest" or "host" or "internal-note" ? normalized : null;
    }

    private bool TryGetTenantContext(out Guid companyId)
    {
        if (!currentTenantContext.IsAuthenticated || currentTenantContext.CompanyId is not { } currentCompanyId || currentCompanyId == Guid.Empty)
        {
            companyId = Guid.Empty;
            return false;
        }

        companyId = currentCompanyId;
        return true;
    }
}
