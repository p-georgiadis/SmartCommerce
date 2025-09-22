using System;
using System.Collections.Generic;

namespace SmartCommerce.Shared.Contracts
{
    /// <summary>
    /// Base class for all domain events
    /// </summary>
    public abstract class DomainEvent
    {
        public Guid Id { get; } = Guid.NewGuid();
        public DateTime OccurredAt { get; } = DateTime.UtcNow;
        public string EventType => GetType().Name;
        public int Version { get; set; } = 1;
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    /// <summary>
    /// Order-related events
    /// </summary>
    public class OrderCreatedEvent : DomainEvent
    {
        public Guid OrderId { get; set; }
        public Guid CustomerId { get; set; }
        public decimal TotalAmount { get; set; }
        public string Currency { get; set; } = "USD";
        public List<OrderItemEvent> Items { get; set; } = new();
        public DateTime OrderDate { get; set; }
        public string Status { get; set; } = "Pending";
    }

    public class OrderUpdatedEvent : DomainEvent
    {
        public Guid OrderId { get; set; }
        public string PreviousStatus { get; set; } = string.Empty;
        public string NewStatus { get; set; } = string.Empty;
        public DateTime UpdatedAt { get; set; }
        public string UpdatedBy { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
    }

    public class OrderCancelledEvent : DomainEvent
    {
        public Guid OrderId { get; set; }
        public Guid CustomerId { get; set; }
        public string CancellationReason { get; set; } = string.Empty;
        public decimal RefundAmount { get; set; }
        public DateTime CancelledAt { get; set; }
    }

    public class OrderShippedEvent : DomainEvent
    {
        public Guid OrderId { get; set; }
        public string TrackingNumber { get; set; } = string.Empty;
        public string ShippingCarrier { get; set; } = string.Empty;
        public DateTime ShippedAt { get; set; }
        public DateTime EstimatedDelivery { get; set; }
        public string ShippingAddress { get; set; } = string.Empty;
    }

    public class OrderItemEvent
    {
        public Guid ProductId { get; set; }
        public int Quantity { get; set; }
        public decimal Price { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string ProductSku { get; set; } = string.Empty;
    }

    /// <summary>
    /// Payment-related events
    /// </summary>
    public class PaymentProcessedEvent : DomainEvent
    {
        public Guid PaymentId { get; set; }
        public Guid OrderId { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "USD";
        public string PaymentMethod { get; set; } = string.Empty;
        public string TransactionId { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime ProcessedAt { get; set; }
    }

    public class PaymentFailedEvent : DomainEvent
    {
        public Guid PaymentId { get; set; }
        public Guid OrderId { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "USD";
        public string FailureReason { get; set; } = string.Empty;
        public string ErrorCode { get; set; } = string.Empty;
        public DateTime FailedAt { get; set; }
    }

    public class RefundProcessedEvent : DomainEvent
    {
        public Guid RefundId { get; set; }
        public Guid PaymentId { get; set; }
        public Guid OrderId { get; set; }
        public decimal RefundAmount { get; set; }
        public string Currency { get; set; } = "USD";
        public string Reason { get; set; } = string.Empty;
        public DateTime ProcessedAt { get; set; }
    }

    /// <summary>
    /// User-related events
    /// </summary>
    public class UserRegisteredEvent : DomainEvent
    {
        public Guid UserId { get; set; }
        public string Email { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public DateTime RegisteredAt { get; set; }
        public string RegistrationSource { get; set; } = "Web";
    }

    public class UserProfileUpdatedEvent : DomainEvent
    {
        public Guid UserId { get; set; }
        public Dictionary<string, object> Changes { get; set; } = new();
        public DateTime UpdatedAt { get; set; }
    }

    public class UserPreferencesUpdatedEvent : DomainEvent
    {
        public Guid UserId { get; set; }
        public Dictionary<string, object> Preferences { get; set; } = new();
        public DateTime UpdatedAt { get; set; }
    }

    /// <summary>
    /// Product/Catalog events
    /// </summary>
    public class ProductCreatedEvent : DomainEvent
    {
        public Guid ProductId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Sku { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string Currency { get; set; } = "USD";
        public string Category { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public class ProductUpdatedEvent : DomainEvent
    {
        public Guid ProductId { get; set; }
        public Dictionary<string, object> Changes { get; set; } = new();
        public DateTime UpdatedAt { get; set; }
    }

    public class ProductPriceChangedEvent : DomainEvent
    {
        public Guid ProductId { get; set; }
        public decimal OldPrice { get; set; }
        public decimal NewPrice { get; set; }
        public string Currency { get; set; } = "USD";
        public string ChangeReason { get; set; } = string.Empty;
        public DateTime ChangedAt { get; set; }
    }

    public class ProductStockUpdatedEvent : DomainEvent
    {
        public Guid ProductId { get; set; }
        public int OldStockLevel { get; set; }
        public int NewStockLevel { get; set; }
        public string UpdateReason { get; set; } = string.Empty;
        public DateTime UpdatedAt { get; set; }
    }

    /// <summary>
    /// Inventory events
    /// </summary>
    public class InventoryReservedEvent : DomainEvent
    {
        public Guid ProductId { get; set; }
        public Guid OrderId { get; set; }
        public int Quantity { get; set; }
        public DateTime ReservedAt { get; set; }
        public string ReservationId { get; set; } = string.Empty;
    }

    public class InventoryReleasedEvent : DomainEvent
    {
        public Guid ProductId { get; set; }
        public Guid OrderId { get; set; }
        public int Quantity { get; set; }
        public DateTime ReleasedAt { get; set; }
        public string ReservationId { get; set; } = string.Empty;
        public string ReleaseReason { get; set; } = string.Empty;
    }

    public class StockLowAlertEvent : DomainEvent
    {
        public Guid ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string Sku { get; set; } = string.Empty;
        public int CurrentStock { get; set; }
        public int MinimumThreshold { get; set; }
        public DateTime AlertTriggeredAt { get; set; }
    }

    /// <summary>
    /// Notification events
    /// </summary>
    public class NotificationRequestedEvent : DomainEvent
    {
        public Guid UserId { get; set; }
        public string NotificationType { get; set; } = string.Empty;
        public string Channel { get; set; } = string.Empty; // Email, SMS, Push, InApp
        public string Subject { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public Dictionary<string, object> Data { get; set; } = new();
        public DateTime RequestedAt { get; set; }
        public int Priority { get; set; } = 1; // 1-5 scale
    }

    public class NotificationSentEvent : DomainEvent
    {
        public Guid NotificationId { get; set; }
        public Guid UserId { get; set; }
        public string Channel { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty; // Sent, Failed, Delivered
        public DateTime SentAt { get; set; }
        public string ExternalId { get; set; } = string.Empty;
    }

    /// <summary>
    /// Search and Analytics events
    /// </summary>
    public class SearchPerformedEvent : DomainEvent
    {
        public string Query { get; set; } = string.Empty;
        public Guid? UserId { get; set; }
        public string SessionId { get; set; } = string.Empty;
        public int ResultsCount { get; set; }
        public string SearchType { get; set; } = "Standard"; // Standard, Semantic, Personalized
        public Dictionary<string, object> Filters { get; set; } = new();
        public int SearchDurationMs { get; set; }
        public DateTime SearchedAt { get; set; }
    }

    public class ProductViewedEvent : DomainEvent
    {
        public Guid ProductId { get; set; }
        public Guid? UserId { get; set; }
        public string SessionId { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty; // Search, Category, Recommendation
        public string ReferrerUrl { get; set; } = string.Empty;
        public int ViewDurationSeconds { get; set; }
        public DateTime ViewedAt { get; set; }
    }

    public class RecommendationGeneratedEvent : DomainEvent
    {
        public Guid? UserId { get; set; }
        public string SessionId { get; set; } = string.Empty;
        public string RecommendationType { get; set; } = string.Empty; // Collaborative, ContentBased, Hybrid
        public List<Guid> RecommendedProductIds { get; set; } = new();
        public string Context { get; set; } = string.Empty; // HomePage, ProductPage, CartPage
        public DateTime GeneratedAt { get; set; }
        public Dictionary<string, object> ModelParameters { get; set; } = new();
    }

    /// <summary>
    /// Fraud Detection events
    /// </summary>
    public class FraudSuspicionEvent : DomainEvent
    {
        public Guid OrderId { get; set; }
        public Guid? UserId { get; set; }
        public string FraudType { get; set; } = string.Empty;
        public double RiskScore { get; set; }
        public string RiskLevel { get; set; } = string.Empty; // Low, Medium, High, Critical
        public List<string> TriggerReasons { get; set; } = new();
        public Dictionary<string, object> Evidence { get; set; } = new();
        public DateTime DetectedAt { get; set; }
        public string ModelVersion { get; set; } = string.Empty;
    }

    public class FraudConfirmedEvent : DomainEvent
    {
        public Guid OrderId { get; set; }
        public Guid? UserId { get; set; }
        public string FraudType { get; set; } = string.Empty;
        public string ConfirmationMethod { get; set; } = string.Empty; // Manual, Automatic
        public string ConfirmedBy { get; set; } = string.Empty;
        public DateTime ConfirmedAt { get; set; }
        public string Actions { get; set; } = string.Empty;
    }
}