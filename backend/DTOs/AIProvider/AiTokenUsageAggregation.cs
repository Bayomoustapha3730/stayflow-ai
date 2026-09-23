namespace StayFlow.Api.DTOs.AIProvider;

public enum OuterOperationType
{
    GuestMessage,
    CopilotOperation
}

public enum AiTokenUsageCompleteness
{
    Unknown,
    Partial,
    Complete
}

public sealed record ProviderCallId
{
    public string Value { get; }

    public ProviderCallId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Provider call id is required.", nameof(value));
        }

        Value = value;
    }

    public static ProviderCallId New() => new(Guid.NewGuid().ToString("N"));

    public override string ToString() => Value;
}

public sealed record ProviderCallContext(
    OuterOperationType OuterOperationType,
    Guid OuterOperationId,
    ProviderCallId ProviderCallId)
{
    public static ProviderCallContext Create(
        OuterOperationType outerOperationType,
        Guid outerOperationId,
        ProviderCallId? providerCallId = null)
    {
        return new ProviderCallContext(outerOperationType, outerOperationId, providerCallId ?? ProviderCallId.New());
    }
}

public sealed class AiTokenUsageAggregation
{
    private readonly Dictionary<string, AiTokenUsage> knownUsage = new(StringComparer.Ordinal);
    private readonly HashSet<string> unknownProviderCallIds = new(StringComparer.Ordinal);

    public AiTokenUsageAggregation(OuterOperationType outerOperationType, Guid outerOperationId)
    {
        OuterOperationType = outerOperationType;
        OuterOperationId = outerOperationId;
    }

    public OuterOperationType OuterOperationType { get; }

    public Guid OuterOperationId { get; }

    public AiTokenUsageCompleteness Completeness
    {
        get
        {
            if (knownUsage.Count == 0 && unknownProviderCallIds.Count == 0)
            {
                return AiTokenUsageCompleteness.Unknown;
            }

            if (knownUsage.Count > 0 && unknownProviderCallIds.Count == 0)
            {
                return AiTokenUsageCompleteness.Complete;
            }

            if (knownUsage.Count > 0 && unknownProviderCallIds.Count > 0)
            {
                return AiTokenUsageCompleteness.Partial;
            }

            return AiTokenUsageCompleteness.Unknown;
        }
    }

    public int ProviderCallCount => knownUsage.Count + unknownProviderCallIds.Count;

    public IReadOnlyDictionary<string, AiTokenUsage> KnownUsageByProviderCallId => knownUsage;

    public IReadOnlyCollection<string> UnknownProviderCallIds => unknownProviderCallIds;

    public AiTokenUsage? Total
    {
        get
        {
            if (Completeness != AiTokenUsageCompleteness.Complete)
            {
                return null;
            }

            try
            {
                var input = checked(knownUsage.Values.Sum(item => item.InputTokens));
                var output = checked(knownUsage.Values.Sum(item => item.OutputTokens));
                var total = checked(knownUsage.Values.Sum(item => item.TotalTokens));
                return new AiTokenUsage(input, output, total);
            }
            catch (OverflowException)
            {
                throw new InvalidOperationException("Token aggregation overflowed the supported numeric range.");
            }
        }
    }

    public static AiTokenUsageAggregation Empty(OuterOperationType outerOperationType, Guid outerOperationId)
    {
        return new AiTokenUsageAggregation(outerOperationType, outerOperationId);
    }

    public void AddKnownUsage(ProviderCallId providerCallId, AiTokenUsage usage)
    {
        ArgumentNullException.ThrowIfNull(providerCallId);
        ArgumentNullException.ThrowIfNull(usage);

        var callId = providerCallId.Value;
        if (knownUsage.TryGetValue(callId, out var existingUsage))
        {
            if (existingUsage == usage)
            {
                return;
            }

            throw new InvalidOperationException($"Conflicting token usage was recorded for provider call '{callId}'.");
        }

        ValidateKnownUsageAddition(usage);
        if (unknownProviderCallIds.Remove(callId))
        {
            knownUsage.Add(callId, usage);
            return;
        }

        knownUsage.Add(callId, usage);
    }

    public void AddUnknownUsage(ProviderCallId providerCallId)
    {
        ArgumentNullException.ThrowIfNull(providerCallId);

        var callId = providerCallId.Value;
        if (knownUsage.ContainsKey(callId) || unknownProviderCallIds.Contains(callId))
        {
            return;
        }

        unknownProviderCallIds.Add(callId);
    }

    public void Merge(AiTokenUsageAggregation other)
    {
        ArgumentNullException.ThrowIfNull(other);

        foreach (var item in other.knownUsage)
        {
            AddKnownUsage(new ProviderCallId(item.Key), item.Value);
        }

        foreach (var item in other.unknownProviderCallIds)
        {
            AddUnknownUsage(new ProviderCallId(item));
        }
    }

    private void ValidateKnownUsageAddition(AiTokenUsage usage)
    {
        try
        {
            var input = checked(knownUsage.Values.Sum(item => item.InputTokens) + usage.InputTokens);
            var output = checked(knownUsage.Values.Sum(item => item.OutputTokens) + usage.OutputTokens);
            var total = checked(knownUsage.Values.Sum(item => item.TotalTokens) + usage.TotalTokens);
            _ = new AiTokenUsage(input, output, total);
        }
        catch (OverflowException)
        {
            throw new InvalidOperationException("Token aggregation overflowed the supported numeric range.");
        }
    }
}
