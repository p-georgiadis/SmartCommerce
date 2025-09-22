using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartCommerce.PaymentService.Models;
using SmartCommerce.PaymentService.Services;
using System.Security.Claims;

namespace SmartCommerce.PaymentService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PaymentController : ControllerBase
{
    private readonly IPaymentService _paymentService;
    private readonly ILogger<PaymentController> _logger;

    public PaymentController(IPaymentService paymentService, ILogger<PaymentController> logger)
    {
        _paymentService = paymentService;
        _logger = logger;
    }

    /// <summary>
    /// Create a new payment
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<Payment>> CreatePayment([FromBody] CreatePaymentRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            // Validate user can create payment for this customer
            var currentUserId = GetCurrentUserId();
            if (!User.IsInRole("Admin") && !User.IsInRole("Manager") && request.CustomerId != currentUserId)
                return Forbid("You can only create payments for yourself");

            var payment = await _paymentService.CreatePaymentAsync(request);
            return CreatedAtAction(nameof(GetPayment), new { id = payment.Id }, payment);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating payment for order {OrderId}", request.OrderId);
            return StatusCode(500, "An error occurred while creating the payment");
        }
    }

    /// <summary>
    /// Get payment by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Payment>> GetPayment(Guid id)
    {
        var payment = await _paymentService.GetPaymentByIdAsync(id);
        if (payment == null)
            return NotFound($"Payment with ID {id} not found");

        // Check if user can access this payment
        var currentUserId = GetCurrentUserId();
        if (!User.IsInRole("Admin") && !User.IsInRole("Manager") && payment.CustomerId != currentUserId)
            return Forbid("You can only access your own payments");

        return Ok(payment);
    }

    /// <summary>
    /// Get payment by order ID
    /// </summary>
    [HttpGet("order/{orderId:guid}")]
    public async Task<ActionResult<Payment>> GetPaymentByOrderId(Guid orderId)
    {
        var payment = await _paymentService.GetPaymentByOrderIdAsync(orderId);
        if (payment == null)
            return NotFound($"Payment for order {orderId} not found");

        // Check if user can access this payment
        var currentUserId = GetCurrentUserId();
        if (!User.IsInRole("Admin") && !User.IsInRole("Manager") && payment.CustomerId != currentUserId)
            return Forbid("You can only access your own payments");

        return Ok(payment);
    }

    /// <summary>
    /// Get payments for the authenticated customer
    /// </summary>
    [HttpGet("my-payments")]
    public async Task<ActionResult<IEnumerable<Payment>>> GetMyPayments(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var customerId = GetCurrentUserId();
        if (string.IsNullOrEmpty(customerId))
            return Unauthorized();

        var payments = await _paymentService.GetPaymentsByCustomerAsync(customerId, page, pageSize);
        return Ok(payments);
    }

    /// <summary>
    /// Get payments by customer ID (admin only)
    /// </summary>
    [HttpGet("customer/{customerId}")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<ActionResult<IEnumerable<Payment>>> GetPaymentsByCustomer(
        string customerId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var payments = await _paymentService.GetPaymentsByCustomerAsync(customerId, page, pageSize);
        return Ok(payments);
    }

    /// <summary>
    /// Get payments by status (admin only)
    /// </summary>
    [HttpGet("status/{status}")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<ActionResult<IEnumerable<Payment>>> GetPaymentsByStatus(
        PaymentStatus status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var payments = await _paymentService.GetPaymentsByStatusAsync(status, page, pageSize);
        return Ok(payments);
    }

    /// <summary>
    /// Process a payment
    /// </summary>
    [HttpPost("{id:guid}/process")]
    public async Task<ActionResult<Payment>> ProcessPayment(Guid id, [FromBody] ProcessPaymentRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var payment = await _paymentService.ProcessPaymentAsync(id, request);
            if (payment == null)
                return NotFound($"Payment with ID {id} not found");

            return Ok(payment);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing payment {PaymentId}", id);
            return StatusCode(500, "An error occurred while processing the payment");
        }
    }

    /// <summary>
    /// Cancel a payment
    /// </summary>
    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<Payment>> CancelPayment(Guid id, [FromBody] CancelPaymentRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var payment = await _paymentService.CancelPaymentAsync(id, request);
            if (payment == null)
                return NotFound($"Payment with ID {id} not found");

            return Ok(payment);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling payment {PaymentId}", id);
            return StatusCode(500, "An error occurred while cancelling the payment");
        }
    }

    /// <summary>
    /// Create a payment intent
    /// </summary>
    [HttpPost("intents")]
    public async Task<ActionResult<PaymentIntent>> CreatePaymentIntent([FromBody] CreatePaymentIntentRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            // Validate user can create payment intent for this customer
            var currentUserId = GetCurrentUserId();
            if (!User.IsInRole("Admin") && !User.IsInRole("Manager") && request.CustomerId != currentUserId)
                return Forbid("You can only create payment intents for yourself");

            var intent = await _paymentService.CreatePaymentIntentAsync(request);
            return CreatedAtAction(nameof(GetPaymentIntent), new { id = intent.Id }, intent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating payment intent for order {OrderId}", request.OrderId);
            return StatusCode(500, "An error occurred while creating the payment intent");
        }
    }

    /// <summary>
    /// Get payment intent by ID
    /// </summary>
    [HttpGet("intents/{id:guid}")]
    public async Task<ActionResult<PaymentIntent>> GetPaymentIntent(Guid id)
    {
        var intent = await _paymentService.GetPaymentIntentByIdAsync(id);
        if (intent == null)
            return NotFound($"Payment intent with ID {id} not found");

        // Check if user can access this payment intent
        var currentUserId = GetCurrentUserId();
        if (!User.IsInRole("Admin") && !User.IsInRole("Manager") && intent.CustomerId != currentUserId)
            return Forbid("You can only access your own payment intents");

        return Ok(intent);
    }

    /// <summary>
    /// Confirm a payment intent
    /// </summary>
    [HttpPost("intents/{id:guid}/confirm")]
    public async Task<ActionResult<PaymentIntent>> ConfirmPaymentIntent(Guid id, [FromBody] ConfirmPaymentRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var intent = await _paymentService.ConfirmPaymentIntentAsync(id, request);
            if (intent == null)
                return NotFound($"Payment intent with ID {id} not found");

            return Ok(intent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error confirming payment intent {IntentId}", id);
            return StatusCode(500, "An error occurred while confirming the payment intent");
        }
    }

    /// <summary>
    /// Cancel a payment intent
    /// </summary>
    [HttpPost("intents/{id:guid}/cancel")]
    public async Task<ActionResult<PaymentIntent>> CancelPaymentIntent(Guid id, [FromBody] CancelPaymentIntentRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var intent = await _paymentService.CancelPaymentIntentAsync(id, request.Reason);
            if (intent == null)
                return NotFound($"Payment intent with ID {id} not found");

            return Ok(intent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling payment intent {IntentId}", id);
            return StatusCode(500, "An error occurred while cancelling the payment intent");
        }
    }

    /// <summary>
    /// Create a refund
    /// </summary>
    [HttpPost("refunds")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<ActionResult<Refund>> CreateRefund([FromBody] CreateRefundRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var refund = await _paymentService.CreateRefundAsync(request);
            return CreatedAtAction(nameof(GetRefund), new { id = refund.Id }, refund);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating refund for payment {PaymentId}", request.PaymentId);
            return StatusCode(500, "An error occurred while creating the refund");
        }
    }

    /// <summary>
    /// Get refund by ID
    /// </summary>
    [HttpGet("refunds/{id:guid}")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<ActionResult<Refund>> GetRefund(Guid id)
    {
        var refund = await _paymentService.GetRefundByIdAsync(id);
        if (refund == null)
            return NotFound($"Refund with ID {id} not found");

        return Ok(refund);
    }

    /// <summary>
    /// Get refunds by payment ID
    /// </summary>
    [HttpGet("{paymentId:guid}/refunds")]
    public async Task<ActionResult<IEnumerable<Refund>>> GetRefundsByPaymentId(Guid paymentId)
    {
        var refunds = await _paymentService.GetRefundsByPaymentIdAsync(paymentId);
        return Ok(refunds);
    }

    /// <summary>
    /// Get payment statistics
    /// </summary>
    [HttpGet("stats")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<ActionResult<Dictionary<PaymentStatus, int>>> GetPaymentStatistics(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        var stats = await _paymentService.GetPaymentStatisticsAsync(startDate, endDate);
        return Ok(stats);
    }

    /// <summary>
    /// Get payment method statistics
    /// </summary>
    [HttpGet("stats/methods")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<ActionResult<Dictionary<PaymentMethod, decimal>>> GetPaymentMethodStats(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        var stats = await _paymentService.GetPaymentMethodStatsAsync(startDate, endDate);
        return Ok(stats);
    }

    /// <summary>
    /// Get total payments amount
    /// </summary>
    [HttpGet("total")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<ActionResult<decimal>> GetTotalPayments(
        [FromQuery] string? customerId = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        var total = await _paymentService.GetTotalPaymentsAsync(customerId, startDate, endDate);
        return Ok(new { TotalAmount = total });
    }

    /// <summary>
    /// Get failed payments
    /// </summary>
    [HttpGet("failed")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<ActionResult<IEnumerable<Payment>>> GetFailedPayments(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var payments = await _paymentService.GetFailedPaymentsAsync(page, pageSize);
        return Ok(payments);
    }

    /// <summary>
    /// Handle payment webhooks
    /// </summary>
    [HttpPost("webhooks")]
    [AllowAnonymous]
    public async Task<ActionResult> HandleWebhook([FromBody] PaymentWebhookRequest webhook)
    {
        try
        {
            var result = await _paymentService.ProcessWebhookAsync(webhook);
            return result ? Ok() : BadRequest("Failed to process webhook");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing webhook {EventType}", webhook.EventType);
            return StatusCode(500, "An error occurred while processing the webhook");
        }
    }

    private string? GetCurrentUserId()
    {
        return User.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
               User.FindFirst("sub")?.Value;
    }
}

public class CancelPaymentIntentRequest
{
    [Required]
    public string Reason { get; set; } = string.Empty;
}