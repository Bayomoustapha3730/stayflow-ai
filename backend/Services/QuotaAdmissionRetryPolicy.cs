using StayFlow.Api.Exceptions;

namespace StayFlow.Api.Services;

internal static class QuotaAdmissionRetryPolicy
{
    public const int MaxAttempts = 3;

    public static async Task<T> ExecuteAsync<T>(
        Func<int, Task<T>> attempt,
        Func<Task> resetAfterFailure,
        Func<Exception, bool> isRetryable,
        CancellationToken cancellationToken)
    {
        for (var attemptNumber = 1; attemptNumber <= MaxAttempts; attemptNumber++)
        {
            try
            {
                return await attempt(attemptNumber);
            }
            catch (Exception exception) when (isRetryable(exception))
            {
                await resetAfterFailure();

                if (attemptNumber == MaxAttempts)
                {
                    throw new ExternalDependencyException(
                        "Quota admission could not be completed.",
                        "quota_admission_unavailable");
                }
            }
        }

        throw new InvalidOperationException("Quota admission retry loop exited unexpectedly.");
    }
}
