using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace SmartCommerce.Shared.Telemetry;

public static class TelemetryExtensions
{
    public static IServiceCollection AddCustomTelemetry(this IServiceCollection services)
    {
        services.AddSingleton<ITelemetryService, TelemetryService>();
        return services;
    }

    public static void TrackBusinessEvent(this ILogger logger, TelemetryClient telemetryClient, string eventName, IDictionary<string, string>? properties = null, IDictionary<string, double>? metrics = null)
    {
        // Log for debugging
        logger.LogInformation("Business Event: {EventName}", eventName);

        // Track in Application Insights
        telemetryClient.TrackEvent(eventName, properties, metrics);
    }

    public static void TrackOrderEvent(this ILogger logger, TelemetryClient telemetryClient, string eventType, Guid orderId, string customerId, decimal amount)
    {
        var properties = new Dictionary<string, string>
        {
            ["OrderId"] = orderId.ToString(),
            ["CustomerId"] = customerId,
            ["EventType"] = eventType
        };

        var metrics = new Dictionary<string, double>
        {
            ["Amount"] = (double)amount
        };

        logger.TrackBusinessEvent(telemetryClient, "OrderEvent", properties, metrics);
    }

    public static void TrackPerformanceMetric(this TelemetryClient telemetryClient, string operationName, TimeSpan duration, bool success = true)
    {
        var dependency = new DependencyTelemetry
        {
            Name = operationName,
            Duration = duration,
            Success = success,
            Timestamp = DateTimeOffset.UtcNow
        };

        telemetryClient.TrackDependency(dependency);
    }

    public static IDisposable BeginOperation(this TelemetryClient telemetryClient, string operationName)
    {
        var operation = telemetryClient.StartOperation<RequestTelemetry>(operationName);
        operation.Telemetry.Start();
        return operation;
    }
}

public interface ITelemetryService
{
    void TrackEvent(string eventName, IDictionary<string, string>? properties = null, IDictionary<string, double>? metrics = null);
    void TrackException(Exception exception, IDictionary<string, string>? properties = null);
    void TrackMetric(string name, double value, IDictionary<string, string>? properties = null);
    void TrackDependency(string dependencyType, string target, string dependencyName, string data, DateTimeOffset startTime, TimeSpan duration, bool success);
    IDisposable StartOperation(string operationName);
}

public class TelemetryService : ITelemetryService
{
    private readonly TelemetryClient _telemetryClient;
    private readonly ILogger<TelemetryService> _logger;

    public TelemetryService(TelemetryClient telemetryClient, ILogger<TelemetryService> logger)
    {
        _telemetryClient = telemetryClient;
        _logger = logger;
    }

    public void TrackEvent(string eventName, IDictionary<string, string>? properties = null, IDictionary<string, double>? metrics = null)
    {
        _logger.LogInformation("Tracking event: {EventName}", eventName);
        _telemetryClient.TrackEvent(eventName, properties, metrics);
    }

    public void TrackException(Exception exception, IDictionary<string, string>? properties = null)
    {
        _logger.LogError(exception, "Tracking exception: {ExceptionType}", exception.GetType().Name);
        _telemetryClient.TrackException(exception, properties);
    }

    public void TrackMetric(string name, double value, IDictionary<string, string>? properties = null)
    {
        _logger.LogInformation("Tracking metric: {MetricName} = {Value}", name, value);
        _telemetryClient.TrackMetric(name, value, properties);
    }

    public void TrackDependency(string dependencyType, string target, string dependencyName, string data, DateTimeOffset startTime, TimeSpan duration, bool success)
    {
        _logger.LogInformation("Tracking dependency: {DependencyName} to {Target} - Success: {Success}, Duration: {Duration}ms",
            dependencyName, target, success, duration.TotalMilliseconds);

        _telemetryClient.TrackDependency(dependencyType, target, dependencyName, data, startTime, duration, success);
    }

    public IDisposable StartOperation(string operationName)
    {
        _logger.LogInformation("Starting operation: {OperationName}", operationName);
        return _telemetryClient.StartOperation<RequestTelemetry>(operationName);
    }
}

public static class ActivityExtensions
{
    public static void AddTag(this Activity? activity, string key, object? value)
    {
        activity?.SetTag(key, value?.ToString());
    }

    public static void AddBaggage(this Activity? activity, string key, string? value)
    {
        if (!string.IsNullOrEmpty(value))
            activity?.SetBaggage(key, value);
    }
}