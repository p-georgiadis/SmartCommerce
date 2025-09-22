namespace SmartCommerce.Shared.Models;

public abstract class EventBase
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Source { get; set; } = Environment.MachineName;
    public string EventType { get; set; } = string.Empty;
    public string Version { get; set; } = "1.0";
    public string CorrelationId { get; set; } = string.Empty;
}

public class OrderCreatedEvent : EventBase
{
    public Guid OrderId { get; set; }
    public string CustomerId { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public List<OrderItemEvent> Items { get; set; } = new();

    public OrderCreatedEvent()
    {
        EventType = nameof(OrderCreatedEvent);
    }
}

public class OrderStatusChangedEvent : EventBase
{
    public Guid OrderId { get; set; }
    public string CustomerId { get; set; } = string.Empty;
    public string OldStatus { get; set; } = string.Empty;
    public string NewStatus { get; set; } = string.Empty;

    public OrderStatusChangedEvent()
    {
        EventType = nameof(OrderStatusChangedEvent);
    }
}

public class OrderCancelledEvent : EventBase
{
    public Guid OrderId { get; set; }
    public string CustomerId { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;

    public OrderCancelledEvent()
    {
        EventType = nameof(OrderCancelledEvent);
    }
}

public class OrderItemEvent
{
    public string ProductId { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal Price { get; set; }
}

public class ProductCreatedEvent : EventBase
{
    public string ProductId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal Price { get; set; }

    public ProductCreatedEvent()
    {
        EventType = nameof(ProductCreatedEvent);
    }
}

public class ProductUpdatedEvent : EventBase
{
    public string ProductId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public Dictionary<string, object> Changes { get; set; } = new();

    public ProductUpdatedEvent()
    {
        EventType = nameof(ProductUpdatedEvent);
    }
}

public class PaymentProcessedEvent : EventBase
{
    public Guid OrderId { get; set; }
    public string CustomerId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public string TransactionId { get; set; } = string.Empty;
    public bool Success { get; set; }

    public PaymentProcessedEvent()
    {
        EventType = nameof(PaymentProcessedEvent);
    }
}

public class UserRegisteredEvent : EventBase
{
    public string UserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string RegistrationMethod { get; set; } = string.Empty;

    public UserRegisteredEvent()
    {
        EventType = nameof(UserRegisteredEvent);
    }
}