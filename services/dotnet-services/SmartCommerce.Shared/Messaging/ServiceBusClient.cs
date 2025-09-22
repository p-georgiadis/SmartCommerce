using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace SmartCommerce.Shared.Messaging;

public class ServiceBusClient : IServiceBusClient
{
    private readonly Azure.Messaging.ServiceBus.ServiceBusClient _client;
    private readonly ILogger<ServiceBusClient> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly Dictionary<string, ServiceBusSender> _senders = new();
    private readonly Dictionary<string, ServiceBusProcessor> _processors = new();
    private bool _disposed;

    public ServiceBusClient(IConfiguration configuration, ILogger<ServiceBusClient> logger)
    {
        var connectionString = configuration.GetConnectionString("ServiceBus") ??
                             configuration["ServiceBus:ConnectionString"];

        if (string.IsNullOrEmpty(connectionString))
            throw new InvalidOperationException("Service Bus connection string is not configured");

        _client = new Azure.Messaging.ServiceBus.ServiceBusClient(connectionString);
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    public async Task PublishEventAsync<T>(string topicOrQueueName, T eventData, CancellationToken cancellationToken = default)
    {
        try
        {
            var sender = GetOrCreateSender(topicOrQueueName);
            var messageBody = JsonSerializer.Serialize(eventData, _jsonOptions);
            var message = new ServiceBusMessage(messageBody)
            {
                ContentType = "application/json",
                MessageId = Guid.NewGuid().ToString(),
                CorrelationId = Activity.Current?.Id ?? Guid.NewGuid().ToString()
            };

            // Add custom properties
            message.ApplicationProperties["EventType"] = typeof(T).Name;
            message.ApplicationProperties["Timestamp"] = DateTimeOffset.UtcNow;
            message.ApplicationProperties["Source"] = Environment.MachineName;

            await sender.SendMessageAsync(message, cancellationToken);
            _logger.LogInformation("Published event {EventType} to {Destination}", typeof(T).Name, topicOrQueueName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish event {EventType} to {Destination}", typeof(T).Name, topicOrQueueName);
            throw;
        }
    }

    public async Task PublishEventAsync(string topicOrQueueName, object eventData, CancellationToken cancellationToken = default)
    {
        try
        {
            var sender = GetOrCreateSender(topicOrQueueName);
            var messageBody = JsonSerializer.Serialize(eventData, _jsonOptions);
            var message = new ServiceBusMessage(messageBody)
            {
                ContentType = "application/json",
                MessageId = Guid.NewGuid().ToString(),
                CorrelationId = Activity.Current?.Id ?? Guid.NewGuid().ToString()
            };

            // Add custom properties
            message.ApplicationProperties["EventType"] = eventData.GetType().Name;
            message.ApplicationProperties["Timestamp"] = DateTimeOffset.UtcNow;
            message.ApplicationProperties["Source"] = Environment.MachineName;

            await sender.SendMessageAsync(message, cancellationToken);
            _logger.LogInformation("Published event {EventType} to {Destination}", eventData.GetType().Name, topicOrQueueName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish event {EventType} to {Destination}", eventData.GetType().Name, topicOrQueueName);
            throw;
        }
    }

    public async Task<IAsyncDisposable> SubscribeAsync<T>(string topicOrQueueName, Func<T, Task> handler, CancellationToken cancellationToken = default)
    {
        var processor = GetOrCreateProcessor(topicOrQueueName);

        processor.ProcessMessageAsync += async args =>
        {
            try
            {
                var messageBody = args.Message.Body.ToString();
                var eventData = JsonSerializer.Deserialize<T>(messageBody, _jsonOptions);

                if (eventData != null)
                {
                    await handler(eventData);
                    await args.CompleteMessageAsync(args.Message, cancellationToken);
                    _logger.LogInformation("Processed message {MessageId} from {Source}", args.Message.MessageId, topicOrQueueName);
                }
                else
                {
                    _logger.LogWarning("Failed to deserialize message {MessageId} to type {Type}", args.Message.MessageId, typeof(T).Name);
                    await args.DeadLetterMessageAsync(args.Message, "DeserializationFailed", "Could not deserialize message to expected type", cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message {MessageId} from {Source}", args.Message.MessageId, topicOrQueueName);
                await args.AbandonMessageAsync(args.Message, cancellationToken: cancellationToken);
            }
        };

        processor.ProcessErrorAsync += args =>
        {
            _logger.LogError(args.Exception, "Error processing messages from {Source}", topicOrQueueName);
            return Task.CompletedTask;
        };

        await processor.StartProcessingAsync(cancellationToken);
        _logger.LogInformation("Started processing messages from {Source}", topicOrQueueName);

        return new ProcessorDisposable(processor, _logger);
    }

    public async Task<IAsyncDisposable> SubscribeAsync(string topicOrQueueName, Func<string, Task> handler, CancellationToken cancellationToken = default)
    {
        var processor = GetOrCreateProcessor(topicOrQueueName);

        processor.ProcessMessageAsync += async args =>
        {
            try
            {
                var messageBody = args.Message.Body.ToString();
                await handler(messageBody);
                await args.CompleteMessageAsync(args.Message, cancellationToken);
                _logger.LogInformation("Processed message {MessageId} from {Source}", args.Message.MessageId, topicOrQueueName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message {MessageId} from {Source}", args.Message.MessageId, topicOrQueueName);
                await args.AbandonMessageAsync(args.Message, cancellationToken: cancellationToken);
            }
        };

        processor.ProcessErrorAsync += args =>
        {
            _logger.LogError(args.Exception, "Error processing messages from {Source}", topicOrQueueName);
            return Task.CompletedTask;
        };

        await processor.StartProcessingAsync(cancellationToken);
        _logger.LogInformation("Started processing messages from {Source}", topicOrQueueName);

        return new ProcessorDisposable(processor, _logger);
    }

    private ServiceBusSender GetOrCreateSender(string destination)
    {
        if (!_senders.TryGetValue(destination, out var sender))
        {
            sender = _client.CreateSender(destination);
            _senders[destination] = sender;
        }
        return sender;
    }

    private ServiceBusProcessor GetOrCreateProcessor(string destination)
    {
        if (!_processors.TryGetValue(destination, out var processor))
        {
            processor = _client.CreateProcessor(destination, new ServiceBusProcessorOptions
            {
                MaxConcurrentCalls = 5,
                AutoCompleteMessages = false,
                MaxAutoLockRenewalDuration = TimeSpan.FromMinutes(10)
            });
            _processors[destination] = processor;
        }
        return processor;
    }

    public void Dispose()
    {
        if (_disposed) return;

        foreach (var sender in _senders.Values)
        {
            sender?.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(30));
        }
        _senders.Clear();

        foreach (var processor in _processors.Values)
        {
            processor?.StopProcessingAsync().Wait(TimeSpan.FromSeconds(30));
            processor?.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(30));
        }
        _processors.Clear();

        _client?.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(30));
        _disposed = true;
    }

    private class ProcessorDisposable : IAsyncDisposable
    {
        private readonly ServiceBusProcessor _processor;
        private readonly ILogger _logger;

        public ProcessorDisposable(ServiceBusProcessor processor, ILogger logger)
        {
            _processor = processor;
            _logger = logger;
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                await _processor.StopProcessingAsync();
                await _processor.DisposeAsync();
                _logger.LogInformation("Stopped message processing");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing message processor");
            }
        }
    }
}