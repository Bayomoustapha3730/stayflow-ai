namespace StayFlow.Api.Exceptions;

public sealed class QuotaExceededException(string metric, long? limit, long requested, long current) : Exception($"Quota exceeded for metric '{metric}'.")
{
    public string Metric { get; } = metric;

    public long? Limit { get; } = limit;

    public long Requested { get; } = requested;

    public long Current { get; } = current;
}