namespace SmartCommerce.Shared.Messaging;

public interface IServiceBusClient : IDisposable
{
    Task PublishEventAsync<T>(string topicOrQueueName, T eventData, CancellationToken cancellationToken = default);
    Task PublishEventAsync(string topicOrQueueName, object eventData, CancellationToken cancellationToken = default);
    Task<IAsyncDisposable> SubscribeAsync<T>(string topicOrQueueName, Func<T, Task> handler, CancellationToken cancellationToken = default);
    Task<IAsyncDisposable> SubscribeAsync(string topicOrQueueName, Func<string, Task> handler, CancellationToken cancellationToken = default);
}