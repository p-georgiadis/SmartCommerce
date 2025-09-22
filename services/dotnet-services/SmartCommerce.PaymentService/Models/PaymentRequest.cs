using System.ComponentModel.DataAnnotations;

namespace SmartCommerce.PaymentService.Models;

public class CreatePaymentRequest
{
    [Required]
    public Guid OrderId { get; set; }

    [Required]
    public string CustomerId { get; set; } = string.Empty;

    [Range(0.01, double.MaxValue)]
    public decimal Amount { get; set; }

    [Required]
    public string Currency { get; set; } = "USD";

    [Required]
    public PaymentMethod Method { get; set; }

    public PaymentProvider Provider { get; set; } = PaymentProvider.Stripe;

    public string? Description { get; set; }

    public Dictionary<string, object> Metadata { get; set; } = new();

    public PaymentMethodDetails? PaymentMethodDetails { get; set; }
}

public class PaymentMethodDetails
{
    public CardDetails? Card { get; set; }
    public BankTransferDetails? BankTransfer { get; set; }
    public WalletDetails? Wallet { get; set; }
}

public class CardDetails
{
    [Required]
    public string Number { get; set; } = string.Empty;

    [Required]
    public string ExpiryMonth { get; set; } = string.Empty;

    [Required]
    public string ExpiryYear { get; set; } = string.Empty;

    [Required]
    public string Cvc { get; set; } = string.Empty;

    public string? HolderName { get; set; }

    public BillingAddress? BillingAddress { get; set; }
}

public class BankTransferDetails
{
    [Required]
    public string AccountNumber { get; set; } = string.Empty;

    [Required]
    public string RoutingNumber { get; set; } = string.Empty;

    public string? AccountHolderName { get; set; }

    public string? BankName { get; set; }
}

public class WalletDetails
{
    [Required]
    public string WalletType { get; set; } = string.Empty; // apple_pay, google_pay, paypal

    public string? WalletToken { get; set; }
}

public class BillingAddress
{
    public string? Line1 { get; set; }
    public string? Line2 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }
}

public class CreatePaymentIntentRequest
{
    [Required]
    public Guid OrderId { get; set; }

    [Required]
    public string CustomerId { get; set; } = string.Empty;

    [Range(0.01, double.MaxValue)]
    public decimal Amount { get; set; }

    [Required]
    public string Currency { get; set; } = "USD";

    public PaymentProvider Provider { get; set; } = PaymentProvider.Stripe;

    public string? Description { get; set; }

    public Dictionary<string, object> Metadata { get; set; } = new();

    public bool AutomaticPaymentMethods { get; set; } = true;
}

public class ConfirmPaymentRequest
{
    [Required]
    public string PaymentMethodId { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
}

public class CreateRefundRequest
{
    [Required]
    public Guid PaymentId { get; set; }

    [Range(0.01, double.MaxValue)]
    public decimal? Amount { get; set; } // null for full refund

    [Required]
    public string Reason { get; set; } = string.Empty;

    public string? Description { get; set; }

    public Dictionary<string, object> Metadata { get; set; } = new();
}

public class ProcessPaymentRequest
{
    [Required]
    public Guid PaymentId { get; set; }

    public string? PaymentMethodToken { get; set; }

    public bool SavePaymentMethod { get; set; }
}

public class CancelPaymentRequest
{
    [Required]
    public string Reason { get; set; } = string.Empty;
}

public class PaymentWebhookRequest
{
    [Required]
    public string EventType { get; set; } = string.Empty;

    [Required]
    public object Data { get; set; } = new();

    public string? Signature { get; set; }

    public DateTime Created { get; set; }
}