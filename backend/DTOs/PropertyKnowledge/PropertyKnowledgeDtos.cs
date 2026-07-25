using StayFlow.Api.Common;
using StayFlow.Api.Models;

namespace StayFlow.Api.DTOs.PropertyKnowledge;

public sealed class PropertyKnowledgeListQuery : PaginationQuery
{
    public string? Search { get; init; }
    public PropertyKnowledgeCategory? Category { get; init; }
    public bool? IsApproved { get; init; }
    public bool? IsActive { get; init; }
}

public sealed class PropertyKnowledgeSummaryResponse
{
    public Guid Id { get; init; }
    public Guid PropertyId { get; init; }
    public string PropertyName { get; init; } = string.Empty;
    public PropertyKnowledgeCategory Category { get; init; }
    public string CategoryLabel { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string? Summary { get; init; }
    public IReadOnlyCollection<string> Tags { get; init; } = [];
    public int Priority { get; init; }
    public bool IsApproved { get; init; }
    public bool IsActive { get; init; }
    public DateTimeOffset? ApprovedAt { get; init; }
    public string? ApprovedBy { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
    public bool CanBeUsedByAI { get; init; }
}

public sealed class PropertyKnowledgeDetailResponse
{
    public Guid Id { get; init; }
    public Guid PropertyId { get; init; }
    public string PropertyName { get; init; } = string.Empty;
    public PropertyKnowledgeCategory Category { get; init; }
    public string CategoryLabel { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string? Summary { get; init; }
    public string Content { get; init; } = string.Empty;
    public IReadOnlyCollection<string> Tags { get; init; } = [];
    public int Priority { get; init; }
    public bool IsApproved { get; init; }
    public bool IsActive { get; init; }
    public DateTimeOffset? ApprovedAt { get; init; }
    public string? ApprovedBy { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
    public int EstimatedCharacterContribution { get; init; }
    public bool CanBeUsedByAI { get; init; }
}

public sealed class CreatePropertyKnowledgeRequest
{
    public PropertyKnowledgeCategory Category { get; init; } = PropertyKnowledgeCategory.Other;
    public string Title { get; init; } = string.Empty;
    public string? Summary { get; init; }
    public string Content { get; init; } = string.Empty;
    public IReadOnlyCollection<string> Tags { get; init; } = [];
    public int Priority { get; init; }
    public bool IsActive { get; init; } = true;
}

public sealed class UpdatePropertyKnowledgeRequest
{
    public PropertyKnowledgeCategory Category { get; init; } = PropertyKnowledgeCategory.Other;
    public string Title { get; init; } = string.Empty;
    public string? Summary { get; init; }
    public string Content { get; init; } = string.Empty;
    public IReadOnlyCollection<string> Tags { get; init; } = [];
    public int Priority { get; init; }
    public bool IsActive { get; init; } = true;
}

public sealed class ChangePropertyKnowledgeApprovalRequest
{
    public bool Approved { get; init; }
}

public sealed class ChangePropertyKnowledgeActiveStateRequest
{
    public bool IsActive { get; init; }
}