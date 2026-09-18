namespace StayFlow.Api.Services;

public interface IWhatsAppOutboundSendCoordinator
{
    Task<T> ExecuteAsync<T>(
        Guid companyId,
        string operationKey,
        Func<CancellationToken, Task<T>> send,
        CancellationToken cancellationToken);
}
