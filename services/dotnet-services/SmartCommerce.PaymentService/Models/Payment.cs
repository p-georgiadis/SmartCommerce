using System.ComponentModel.DataAnnotations;

namespace SmartCommerce.PaymentService.Models;

public class Payment
{
    public Guid Id { get; set; }

    [Required]
    public Guid OrderId { get; set; }

    [Required]
    public string CustomerId { get; set; } = string.Empty;

    [Range(0.01, double.MaxValue)]
    public decimal Amount { get; set; }

    [Required]
    public string Currency { get; set; } = "USD";

    public PaymentStatus Status { get; set; }

    [Required]
    public PaymentMethod Method { get; set; }

    public string? TransactionId { get; set; }

    public string? ExternalTransactionId { get; set; }

    public string? FailureReason { get; set; }

    public string? Description { get; set; }

    public Dictionary<string, object> Metadata { get; set; } = new();

    public DateTime CreatedAt { get; set; }

    public DateTime? ProcessedAt { get; set; }

    public DateTime? RefundedAt { get; set; }

    public decimal RefundedAmount { get; set; }

    public PaymentProvider Provider { get; set; }

    public string? ProviderPaymentId { get; set; }

    public string? ReceiptUrl { get; set; }
}

public enum PaymentStatus
{
    Pending = 0,
    Processing = 1,
    Succeeded = 2,
    Failed = 3,
    Cancelled = 4,
    Refunded = 5,
    PartiallyRefunded = 6
}

public enum PaymentMethod
{
    CreditCard = 0,
    DebitCard = 1,
    BankTransfer = 2,
    PayPal = 3,
    ApplePay = 4,
    GooglePay = 5,
    Cryptocurrency = 6,
    Cash = 7,
    GiftCard = 8
}

public enum PaymentProvider
{
    Stripe = 0,
    PayPal = 1,
    Square = 2,
    Adyen = 3,
    Braintree = 4,
    Internal = 5
}

public class PaymentIntent
{
    public Guid Id { get; set; }

    [Required]
    public Guid OrderId { get; set; }

    [Required]
    public string CustomerId { get; set; } = string.Empty;

    [Range(0.01, double.MaxValue)]
    public decimal Amount { get; set; }

    [Required]
    public string Currency { get; set; } = "USD";

    public PaymentIntentStatus Status { get; set; }

    public string? ClientSecret { get; set; }

    public string? ProviderIntentId { get; set; }

    public PaymentProvider Provider { get; set; }

    public Dictionary<string, object> Metadata { get; set; } = new();

    public DateTime CreatedAt { get; set; }

    public DateTime? ConfirmedAt { get; set; }

    public DateTime ExpiresAt { get; set; }
}

public enum PaymentIntentStatus
{
    Created = 0,
    RequiresPaymentMethod = 1,
    RequiresConfirmation = 2,
    RequiresAction = 3,
    Processing = 4,
    Succeeded = 5,
    Cancelled = 6
}

public class Refund
{
    public Guid Id { get; set; }

    [Required]
    public Guid PaymentId { get; set; }

    [Range(0.01, double.MaxValue)]
    public decimal Amount { get; set; }

    [Required]
    public string Currency { get; set; } = "USD";

    public RefundStatus Status { get; set; }

    [Required]
    public string Reason { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? ExternalRefundId { get; set; }

    public string? FailureReason { get; set; }

    public Dictionary<string, object> Metadata { get; set; } = new();

    public DateTime CreatedAt { get; set; }

    public DateTime? ProcessedAt { get; set; }

    public Payment Payment { get; set; } = null!;
}

public enum RefundStatus
{
    Pending = 0,
    Processing = 1,
    Succeeded = 2,
    Failed = 3,
    Cancelled = 4
}