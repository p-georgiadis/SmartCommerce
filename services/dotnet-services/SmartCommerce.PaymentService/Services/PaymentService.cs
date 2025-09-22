using Microsoft.EntityFrameworkCore;
using SmartCommerce.PaymentService.Data;
using SmartCommerce.PaymentService.Models;
using SmartCommerce.Shared.Messaging;
using Stripe;

namespace SmartCommerce.PaymentService.Services;

public class PaymentService : IPaymentService
{
    private readonly PaymentDbContext _context;
    private readonly ILogger<PaymentService> _logger;
    private readonly IServiceBusClient _serviceBus;
    private readonly PaymentIntentService _stripePaymentIntentService;
    private readonly RefundService _stripeRefundService;

    public PaymentService(
        PaymentDbContext context,
        ILogger<PaymentService> logger,
        IServiceBusClient serviceBus)
    {
        _context = context;
        _logger = logger;
        _serviceBus = serviceBus;
        _stripePaymentIntentService = new PaymentIntentService();
        _stripeRefundService = new RefundService();
    }

    public async Task<Payment> CreatePaymentAsync(CreatePaymentRequest request)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            // Validate request
            await ValidatePaymentRequestAsync(request);

            var payment = new Payment
            {
                Id = Guid.NewGuid(),
                OrderId = request.OrderId,
                CustomerId = request.CustomerId,
                Amount = request.Amount,
                Currency = request.Currency.ToUpper(),
                Status = PaymentStatus.Pending,
                Method = request.Method,
                Provider = request.Provider,
                Description = request.Description,
                Metadata = request.Metadata,
                CreatedAt = DateTime.UtcNow,
                TransactionId = GenerateTransactionId()
            };

            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            // Publish payment created event
            await PublishPaymentCreatedEventAsync(payment);

            _logger.LogInformation("Payment {PaymentId} created for order {OrderId}", payment.Id, payment.OrderId);
            return payment;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error creating payment for order {OrderId}", request.OrderId);
            throw;
        }
    }

    public async Task<Payment?> GetPaymentByIdAsync(Guid id)
    {
        return await _context.Payments.FindAsync(id);
    }

    public async Task<Payment?> GetPaymentByOrderIdAsync(Guid orderId)
    {
        return await _context.Payments
            .FirstOrDefaultAsync(p => p.OrderId == orderId);
    }

    public async Task<IEnumerable<Payment>> GetPaymentsByCustomerAsync(string customerId, int page = 1, int pageSize = 20)
    {
        return await _context.Payments
            .Where(p => p.CustomerId == customerId)
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<Payment?> ProcessPaymentAsync(Guid paymentId, ProcessPaymentRequest request)
    {
        var payment = await _context.Payments.FindAsync(paymentId);
        if (payment == null)
            return null;

        if (payment.Status != PaymentStatus.Pending)
            throw new InvalidOperationException($"Payment {paymentId} is not in pending status");

        try
        {
            payment.Status = PaymentStatus.Processing;
            await _context.SaveChangesAsync();

            // Process payment based on provider
            var result = await ProcessPaymentWithProviderAsync(payment, request);

            payment.Status = result.Success ? PaymentStatus.Succeeded : PaymentStatus.Failed;
            payment.ExternalTransactionId = result.ExternalTransactionId;
            payment.ProviderPaymentId = result.ProviderPaymentId;
            payment.FailureReason = result.FailureReason;
            payment.ReceiptUrl = result.ReceiptUrl;
            payment.ProcessedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            // Publish payment processed event
            await PublishPaymentProcessedEventAsync(payment);

            _logger.LogInformation("Payment {PaymentId} processed with status {Status}", paymentId, payment.Status);
            return payment;
        }
        catch (Exception ex)
        {
            payment.Status = PaymentStatus.Failed;
            payment.FailureReason = ex.Message;
            await _context.SaveChangesAsync();

            _logger.LogError(ex, "Error processing payment {PaymentId}", paymentId);
            throw;
        }
    }

    public async Task<Payment?> CancelPaymentAsync(Guid paymentId, CancelPaymentRequest request)
    {
        var payment = await _context.Payments.FindAsync(paymentId);
        if (payment == null)
            return null;

        if (payment.Status != PaymentStatus.Pending && payment.Status != PaymentStatus.Processing)
            throw new InvalidOperationException($"Payment {paymentId} cannot be cancelled in {payment.Status} status");

        payment.Status = PaymentStatus.Cancelled;
        payment.FailureReason = request.Reason;
        await _context.SaveChangesAsync();

        // Publish payment cancelled event
        await PublishPaymentCancelledEventAsync(payment, request.Reason);

        _logger.LogInformation("Payment {PaymentId} cancelled. Reason: {Reason}", paymentId, request.Reason);
        return payment;
    }

    public async Task<PaymentIntent> CreatePaymentIntentAsync(CreatePaymentIntentRequest request)
    {
        try
        {
            var intent = new PaymentIntent
            {
                Id = Guid.NewGuid(),
                OrderId = request.OrderId,
                CustomerId = request.CustomerId,
                Amount = request.Amount,
                Currency = request.Currency.ToUpper(),
                Status = PaymentIntentStatus.Created,
                Provider = request.Provider,
                Metadata = request.Metadata,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(1)
            };

            // Create provider-specific payment intent
            if (request.Provider == PaymentProvider.Stripe)
            {
                var stripeIntent = await CreateStripePaymentIntentAsync(request);
                intent.ProviderIntentId = stripeIntent.Id;
                intent.ClientSecret = stripeIntent.ClientSecret;
                intent.Status = MapStripeStatusToIntentStatus(stripeIntent.Status);
            }

            _context.PaymentIntents.Add(intent);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Payment intent {IntentId} created for order {OrderId}", intent.Id, intent.OrderId);
            return intent;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating payment intent for order {OrderId}", request.OrderId);
            throw;
        }
    }

    public async Task<PaymentIntent?> GetPaymentIntentByIdAsync(Guid id)
    {
        return await _context.PaymentIntents.FindAsync(id);
    }

    public async Task<PaymentIntent?> ConfirmPaymentIntentAsync(Guid intentId, ConfirmPaymentRequest request)
    {
        var intent = await _context.PaymentIntents.FindAsync(intentId);
        if (intent == null)
            return null;

        try
        {
            if (intent.Provider == PaymentProvider.Stripe && !string.IsNullOrEmpty(intent.ProviderIntentId))
            {
                var confirmOptions = new PaymentIntentConfirmOptions
                {
                    PaymentMethod = request.PaymentMethodId,
                    ReturnUrl = request.ReturnUrl
                };

                var stripeIntent = await _stripePaymentIntentService.ConfirmAsync(intent.ProviderIntentId, confirmOptions);
                intent.Status = MapStripeStatusToIntentStatus(stripeIntent.Status);
                intent.ConfirmedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Payment intent {IntentId} confirmed with status {Status}", intentId, intent.Status);
            return intent;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error confirming payment intent {IntentId}", intentId);
            throw;
        }
    }

    public async Task<PaymentIntent?> CancelPaymentIntentAsync(Guid intentId, string reason)
    {
        var intent = await _context.PaymentIntents.FindAsync(intentId);
        if (intent == null)
            return null;

        try
        {
            if (intent.Provider == PaymentProvider.Stripe && !string.IsNullOrEmpty(intent.ProviderIntentId))
            {
                await _stripePaymentIntentService.CancelAsync(intent.ProviderIntentId);
            }

            intent.Status = PaymentIntentStatus.Cancelled;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Payment intent {IntentId} cancelled. Reason: {Reason}", intentId, reason);
            return intent;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling payment intent {IntentId}", intentId);
            throw;
        }
    }

    public async Task<Refund> CreateRefundAsync(CreateRefundRequest request)
    {
        var payment = await _context.Payments.FindAsync(request.PaymentId);
        if (payment == null)
            throw new ArgumentException($"Payment {request.PaymentId} not found");

        if (payment.Status != PaymentStatus.Succeeded)
            throw new InvalidOperationException($"Cannot refund payment in {payment.Status} status");

        var refundAmount = request.Amount ?? payment.Amount;
        if (refundAmount > payment.Amount - payment.RefundedAmount)
            throw new InvalidOperationException("Refund amount exceeds available refund amount");

        try
        {
            var refund = new Refund
            {
                Id = Guid.NewGuid(),
                PaymentId = request.PaymentId,
                Amount = refundAmount,
                Currency = payment.Currency,
                Status = RefundStatus.Pending,
                Reason = request.Reason,
                Description = request.Description,
                Metadata = request.Metadata,
                CreatedAt = DateTime.UtcNow,
                Payment = payment
            };

            _context.Refunds.Add(refund);
            await _context.SaveChangesAsync();

            // Process refund immediately
            await ProcessRefundAsync(refund.Id);

            _logger.LogInformation("Refund {RefundId} created for payment {PaymentId}", refund.Id, request.PaymentId);
            return refund;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating refund for payment {PaymentId}", request.PaymentId);
            throw;
        }
    }

    public async Task<Refund?> GetRefundByIdAsync(Guid id)
    {
        return await _context.Refunds
            .Include(r => r.Payment)
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task<IEnumerable<Refund>> GetRefundsByPaymentIdAsync(Guid paymentId)
    {
        return await _context.Refunds
            .Where(r => r.PaymentId == paymentId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
    }

    public async Task<bool> ProcessRefundAsync(Guid refundId)
    {
        var refund = await _context.Refunds
            .Include(r => r.Payment)
            .FirstOrDefaultAsync(r => r.Id == refundId);

        if (refund == null)
            return false;

        try
        {
            refund.Status = RefundStatus.Processing;
            await _context.SaveChangesAsync();

            // Process refund with provider
            var result = await ProcessRefundWithProviderAsync(refund);

            refund.Status = result.Success ? RefundStatus.Succeeded : RefundStatus.Failed;
            refund.ExternalRefundId = result.ExternalRefundId;
            refund.FailureReason = result.FailureReason;
            refund.ProcessedAt = DateTime.UtcNow;

            // Update payment refunded amount
            if (result.Success)
            {
                refund.Payment.RefundedAmount += refund.Amount;
                if (refund.Payment.RefundedAmount >= refund.Payment.Amount)
                {
                    refund.Payment.Status = PaymentStatus.Refunded;
                }
                else
                {
                    refund.Payment.Status = PaymentStatus.PartiallyRefunded;
                }
            }

            await _context.SaveChangesAsync();

            // Publish refund processed event
            await PublishRefundProcessedEventAsync(refund);

            _logger.LogInformation("Refund {RefundId} processed with status {Status}", refundId, refund.Status);
            return result.Success;
        }
        catch (Exception ex)
        {
            refund.Status = RefundStatus.Failed;
            refund.FailureReason = ex.Message;
            await _context.SaveChangesAsync();

            _logger.LogError(ex, "Error processing refund {RefundId}", refundId);
            return false;
        }
    }

    public async Task<bool> UpdatePaymentStatusAsync(Guid paymentId, PaymentStatus status, string? reason = null)
    {
        var payment = await _context.Payments.FindAsync(paymentId);
        if (payment == null)
            return false;

        payment.Status = status;
        if (!string.IsNullOrEmpty(reason))
            payment.FailureReason = reason;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Payment {PaymentId} status updated to {Status}", paymentId, status);
        return true;
    }

    public async Task<IEnumerable<Payment>> GetPaymentsByStatusAsync(PaymentStatus status, int page = 1, int pageSize = 20)
    {
        return await _context.Payments
            .Where(p => p.Status == status)
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<decimal> GetTotalPaymentsAsync(string? customerId = null, DateTime? startDate = null, DateTime? endDate = null)
    {
        var query = _context.Payments.Where(p => p.Status == PaymentStatus.Succeeded);

        if (!string.IsNullOrEmpty(customerId))
            query = query.Where(p => p.CustomerId == customerId);

        if (startDate.HasValue)
            query = query.Where(p => p.CreatedAt >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(p => p.CreatedAt <= endDate.Value);

        return await query.SumAsync(p => p.Amount);
    }

    public async Task<bool> ProcessWebhookAsync(PaymentWebhookRequest webhook)
    {
        try
        {
            _logger.LogInformation("Processing webhook event {EventType}", webhook.EventType);

            // Handle different webhook events based on provider
            switch (webhook.EventType)
            {
                case "payment_intent.succeeded":
                    await HandlePaymentSucceededWebhookAsync(webhook.Data);
                    break;
                case "payment_intent.payment_failed":
                    await HandlePaymentFailedWebhookAsync(webhook.Data);
                    break;
                case "charge.dispute.created":
                    await HandleChargeDisputeWebhookAsync(webhook.Data);
                    break;
                default:
                    _logger.LogInformation("Unhandled webhook event type: {EventType}", webhook.EventType);
                    break;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing webhook {EventType}", webhook.EventType);
            return false;
        }
    }

    public async Task<Dictionary<PaymentStatus, int>> GetPaymentStatisticsAsync(DateTime? startDate = null, DateTime? endDate = null)
    {
        var query = _context.Payments.AsQueryable();

        if (startDate.HasValue)
            query = query.Where(p => p.CreatedAt >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(p => p.CreatedAt <= endDate.Value);

        return await query
            .GroupBy(p => p.Status)
            .ToDictionaryAsync(g => g.Key, g => g.Count());
    }

    public async Task<Dictionary<PaymentMethod, decimal>> GetPaymentMethodStatsAsync(DateTime? startDate = null, DateTime? endDate = null)
    {
        var query = _context.Payments.Where(p => p.Status == PaymentStatus.Succeeded);

        if (startDate.HasValue)
            query = query.Where(p => p.CreatedAt >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(p => p.CreatedAt <= endDate.Value);

        return await query
            .GroupBy(p => p.Method)
            .ToDictionaryAsync(g => g.Key, g => g.Sum(p => p.Amount));
    }

    public async Task<IEnumerable<Payment>> GetFailedPaymentsAsync(int page = 1, int pageSize = 20)
    {
        return await _context.Payments
            .Where(p => p.Status == PaymentStatus.Failed)
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<bool> ValidatePaymentAsync(Payment payment)
    {
        // Implement payment validation logic
        return await Task.FromResult(true);
    }

    public async Task<bool> IsPaymentSuspiciousAsync(CreatePaymentRequest request)
    {
        // Implement fraud detection logic
        return await Task.FromResult(false);
    }

    // Private helper methods
    private async Task ValidatePaymentRequestAsync(CreatePaymentRequest request)
    {
        if (await IsPaymentSuspiciousAsync(request))
        {
            throw new InvalidOperationException("Payment flagged as suspicious");
        }

        // Additional validation logic
    }

    private string GenerateTransactionId()
    {
        return $"txn_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}_{Random.Shared.Next(1000, 9999)}";
    }

    private async Task<PaymentProcessResult> ProcessPaymentWithProviderAsync(Payment payment, ProcessPaymentRequest request)
    {
        // Mock implementation - in real scenario, integrate with actual payment providers
        await Task.Delay(100); // Simulate processing delay

        return new PaymentProcessResult
        {
            Success = true,
            ExternalTransactionId = $"ext_{Guid.NewGuid()}",
            ProviderPaymentId = $"pi_{Guid.NewGuid()}",
            ReceiptUrl = $"https://receipts.example.com/{payment.Id}"
        };
    }

    private async Task<RefundProcessResult> ProcessRefundWithProviderAsync(Refund refund)
    {
        // Mock implementation
        await Task.Delay(100);

        return new RefundProcessResult
        {
            Success = true,
            ExternalRefundId = $"ref_{Guid.NewGuid()}"
        };
    }

    private async Task<Stripe.PaymentIntent> CreateStripePaymentIntentAsync(CreatePaymentIntentRequest request)
    {
        var options = new PaymentIntentCreateOptions
        {
            Amount = (long)(request.Amount * 100), // Stripe uses cents
            Currency = request.Currency.ToLower(),
            Description = request.Description,
            Metadata = request.Metadata.ToDictionary(x => x.Key, x => x.Value.ToString() ?? "")
        };

        return await _stripePaymentIntentService.CreateAsync(options);
    }

    private PaymentIntentStatus MapStripeStatusToIntentStatus(string stripeStatus)
    {
        return stripeStatus switch
        {
            "requires_payment_method" => PaymentIntentStatus.RequiresPaymentMethod,
            "requires_confirmation" => PaymentIntentStatus.RequiresConfirmation,
            "requires_action" => PaymentIntentStatus.RequiresAction,
            "processing" => PaymentIntentStatus.Processing,
            "succeeded" => PaymentIntentStatus.Succeeded,
            "canceled" => PaymentIntentStatus.Cancelled,
            _ => PaymentIntentStatus.Created
        };
    }

    private async Task HandlePaymentSucceededWebhookAsync(object data)
    {
        // Handle successful payment webhook
        _logger.LogInformation("Payment succeeded webhook received");
    }

    private async Task HandlePaymentFailedWebhookAsync(object data)
    {
        // Handle failed payment webhook
        _logger.LogInformation("Payment failed webhook received");
    }

    private async Task HandleChargeDisputeWebhookAsync(object data)
    {
        // Handle chargeback/dispute webhook
        _logger.LogInformation("Charge dispute webhook received");
    }

    private async Task PublishPaymentCreatedEventAsync(Payment payment)
    {
        var paymentEvent = new
        {
            EventType = "PaymentCreated",
            PaymentId = payment.Id,
            OrderId = payment.OrderId,
            CustomerId = payment.CustomerId,
            Amount = payment.Amount,
            Currency = payment.Currency,
            Method = payment.Method.ToString(),
            Status = payment.Status.ToString(),
            Timestamp = DateTime.UtcNow
        };

        await _serviceBus.PublishEventAsync("payment-events", paymentEvent);
    }

    private async Task PublishPaymentProcessedEventAsync(Payment payment)
    {
        var paymentEvent = new
        {
            EventType = "PaymentProcessed",
            PaymentId = payment.Id,
            OrderId = payment.OrderId,
            CustomerId = payment.CustomerId,
            Amount = payment.Amount,
            Status = payment.Status.ToString(),
            Success = payment.Status == PaymentStatus.Succeeded,
            Timestamp = DateTime.UtcNow
        };

        await _serviceBus.PublishEventAsync("payment-events", paymentEvent);
    }

    private async Task PublishPaymentCancelledEventAsync(Payment payment, string reason)
    {
        var paymentEvent = new
        {
            EventType = "PaymentCancelled",
            PaymentId = payment.Id,
            OrderId = payment.OrderId,
            CustomerId = payment.CustomerId,
            Reason = reason,
            Timestamp = DateTime.UtcNow
        };

        await _serviceBus.PublishEventAsync("payment-events", paymentEvent);
    }

    private async Task PublishRefundProcessedEventAsync(Refund refund)
    {
        var refundEvent = new
        {
            EventType = "RefundProcessed",
            RefundId = refund.Id,
            PaymentId = refund.PaymentId,
            Amount = refund.Amount,
            Status = refund.Status.ToString(),
            Success = refund.Status == RefundStatus.Succeeded,
            Timestamp = DateTime.UtcNow
        };

        await _serviceBus.PublishEventAsync("payment-events", refundEvent);
    }
}

public class PaymentProcessResult
{
    public bool Success { get; set; }
    public string? ExternalTransactionId { get; set; }
    public string? ProviderPaymentId { get; set; }
    public string? FailureReason { get; set; }
    public string? ReceiptUrl { get; set; }
}

public class RefundProcessResult
{
    public bool Success { get; set; }
    public string? ExternalRefundId { get; set; }
    public string? FailureReason { get; set; }
}