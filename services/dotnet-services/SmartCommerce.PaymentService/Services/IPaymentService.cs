using SmartCommerce.PaymentService.Models;

namespace SmartCommerce.PaymentService.Services;

public interface IPaymentService
{
    // Payment operations
    Task<Payment> CreatePaymentAsync(CreatePaymentRequest request);
    Task<Payment?> GetPaymentByIdAsync(Guid id);
    Task<Payment?> GetPaymentByOrderIdAsync(Guid orderId);
    Task<IEnumerable<Payment>> GetPaymentsByCustomerAsync(string customerId, int page = 1, int pageSize = 20);
    Task<Payment?> ProcessPaymentAsync(Guid paymentId, ProcessPaymentRequest request);
    Task<Payment?> CancelPaymentAsync(Guid paymentId, CancelPaymentRequest request);

    // Payment Intents
    Task<PaymentIntent> CreatePaymentIntentAsync(CreatePaymentIntentRequest request);
    Task<PaymentIntent?> GetPaymentIntentByIdAsync(Guid id);
    Task<PaymentIntent?> ConfirmPaymentIntentAsync(Guid intentId, ConfirmPaymentRequest request);
    Task<PaymentIntent?> CancelPaymentIntentAsync(Guid intentId, string reason);

    // Refunds
    Task<Refund> CreateRefundAsync(CreateRefundRequest request);
    Task<Refund?> GetRefundByIdAsync(Guid id);
    Task<IEnumerable<Refund>> GetRefundsByPaymentIdAsync(Guid paymentId);
    Task<bool> ProcessRefundAsync(Guid refundId);

    // Payment status and tracking
    Task<bool> UpdatePaymentStatusAsync(Guid paymentId, PaymentStatus status, string? reason = null);
    Task<IEnumerable<Payment>> GetPaymentsByStatusAsync(PaymentStatus status, int page = 1, int pageSize = 20);
    Task<decimal> GetTotalPaymentsAsync(string? customerId = null, DateTime? startDate = null, DateTime? endDate = null);

    // Webhook handling
    Task<bool> ProcessWebhookAsync(PaymentWebhookRequest webhook);

    // Analytics and reporting
    Task<Dictionary<PaymentStatus, int>> GetPaymentStatisticsAsync(DateTime? startDate = null, DateTime? endDate = null);
    Task<Dictionary<PaymentMethod, decimal>> GetPaymentMethodStatsAsync(DateTime? startDate = null, DateTime? endDate = null);
    Task<IEnumerable<Payment>> GetFailedPaymentsAsync(int page = 1, int pageSize = 20);

    // Security and fraud detection
    Task<bool> ValidatePaymentAsync(Payment payment);
    Task<bool> IsPaymentSuspiciousAsync(CreatePaymentRequest request);
}