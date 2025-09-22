using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartCommerce.OrderService.Models;
using SmartCommerce.OrderService.Services;
using System.Security.Claims;

namespace SmartCommerce.OrderService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OrderController : ControllerBase
{
    private readonly IOrderService _orderService;
    private readonly ILogger<OrderController> _logger;

    public OrderController(IOrderService orderService, ILogger<OrderController> logger)
    {
        _orderService = orderService;
        _logger = logger;
    }

    /// <summary>
    /// Get all orders with pagination
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Order>>> GetOrders(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        if (page < 1 || pageSize < 1 || pageSize > 100)
            return BadRequest("Invalid pagination parameters");

        var orders = await _orderService.GetOrdersAsync(page, pageSize);
        return Ok(orders);
    }

    /// <summary>
    /// Get order by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Order>> GetOrder(Guid id)
    {
        var order = await _orderService.GetOrderByIdAsync(id);
        if (order == null)
            return NotFound($"Order with ID {id} not found");

        return Ok(order);
    }

    /// <summary>
    /// Get orders for the authenticated customer
    /// </summary>
    [HttpGet("my-orders")]
    public async Task<ActionResult<IEnumerable<Order>>> GetMyOrders(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var customerId = GetCurrentUserId();
        if (string.IsNullOrEmpty(customerId))
            return Unauthorized();

        var orders = await _orderService.GetOrdersByCustomerAsync(customerId, page, pageSize);
        return Ok(orders);
    }

    /// <summary>
    /// Get orders by customer ID (admin only)
    /// </summary>
    [HttpGet("customer/{customerId}")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<ActionResult<IEnumerable<Order>>> GetOrdersByCustomer(
        string customerId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        if (string.IsNullOrEmpty(customerId))
            return BadRequest("Customer ID is required");

        var orders = await _orderService.GetOrdersByCustomerAsync(customerId, page, pageSize);
        return Ok(orders);
    }

    /// <summary>
    /// Get orders by status (admin only)
    /// </summary>
    [HttpGet("status/{status}")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<ActionResult<IEnumerable<Order>>> GetOrdersByStatus(
        OrderStatus status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var orders = await _orderService.GetOrdersByStatusAsync(status, page, pageSize);
        return Ok(orders);
    }

    /// <summary>
    /// Create a new order
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<Order>> CreateOrder([FromBody] CreateOrderRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        // Ensure customer can only create orders for themselves
        var currentUserId = GetCurrentUserId();
        if (!User.IsInRole("Admin") && !User.IsInRole("Manager") && request.CustomerId != currentUserId)
            return Forbid("You can only create orders for yourself");

        try
        {
            var order = await _orderService.CreateOrderAsync(request);
            return CreatedAtAction(nameof(GetOrder), new { id = order.Id }, order);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to create order for customer {CustomerId}", request.CustomerId);
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating order for customer {CustomerId}", request.CustomerId);
            return StatusCode(500, "An error occurred while creating the order");
        }
    }

    /// <summary>
    /// Update order status
    /// </summary>
    [HttpPut("{id:guid}/status")]
    [Authorize(Roles = "Admin,Manager,Fulfillment")]
    public async Task<ActionResult> UpdateOrderStatus(Guid id, [FromBody] UpdateOrderStatusRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var result = await _orderService.UpdateOrderStatusAsync(id, request.Status, request.Notes);
            if (!result)
                return NotFound($"Order with ID {id} not found");

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating status for order {OrderId}", id);
            return StatusCode(500, "An error occurred while updating the order status");
        }
    }

    /// <summary>
    /// Cancel an order
    /// </summary>
    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult> CancelOrder(Guid id, [FromBody] CancelOrderRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            // Check if user can cancel this order
            var order = await _orderService.GetOrderByIdAsync(id);
            if (order == null)
                return NotFound($"Order with ID {id} not found");

            var currentUserId = GetCurrentUserId();
            if (!User.IsInRole("Admin") && !User.IsInRole("Manager") && order.CustomerId != currentUserId)
                return Forbid("You can only cancel your own orders");

            var result = await _orderService.CancelOrderAsync(id, request.Reason);
            if (!result)
                return BadRequest("Order cannot be cancelled");

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling order {OrderId}", id);
            return StatusCode(500, "An error occurred while cancelling the order");
        }
    }

    /// <summary>
    /// Get order total
    /// </summary>
    [HttpGet("{id:guid}/total")]
    public async Task<ActionResult<decimal>> GetOrderTotal(Guid id)
    {
        var total = await _orderService.GetOrderTotalAsync(id);
        if (total == 0)
            return NotFound($"Order with ID {id} not found");

        return Ok(new { OrderId = id, Total = total });
    }

    private string? GetCurrentUserId()
    {
        return User.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
               User.FindFirst("sub")?.Value;
    }
}

public class CancelOrderRequest
{
    public string Reason { get; set; } = string.Empty;
}