using Microsoft.EntityFrameworkCore;
using SmartCommerce.OrderService.Data;
using SmartCommerce.OrderService.Models;
using SmartCommerce.Shared.Messaging;

namespace SmartCommerce.OrderService.Services;

public class OrderService : IOrderService
{
    private readonly OrderDbContext _context;
    private readonly ILogger<OrderService> _logger;
    private readonly IServiceBusClient _serviceBus;

    public OrderService(OrderDbContext context, ILogger<OrderService> logger, IServiceBusClient serviceBus)
    {
        _context = context;
        _logger = logger;
        _serviceBus = serviceBus;
    }

    public async Task<Order> CreateOrderAsync(CreateOrderRequest request)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            var order = new Order
            {
                Id = Guid.NewGuid(),
                CustomerId = request.CustomerId,
                OrderDate = DateTime.UtcNow,
                Status = OrderStatus.Pending,
                Notes = request.Notes,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Items = request.Items.Select(i => new OrderItem
                {
                    Id = Guid.NewGuid(),
                    ProductId = i.ProductId,
                    ProductName = await GetProductNameAsync(i.ProductId),
                    Quantity = i.Quantity,
                    Price = i.Price,
                    Discount = i.Discount
                }).ToList()
            };

            order.TotalAmount = order.Items.Sum(i => (i.Price - i.Discount) * i.Quantity);

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            // Verify inventory availability
            var inventoryCheck = await CheckInventoryAsync(order.Items);
            if (!inventoryCheck.Success)
            {
                throw new InvalidOperationException($"Insufficient inventory for products: {string.Join(", ", inventoryCheck.UnavailableItems)}");
            }

            await transaction.CommitAsync();

            // Publish order created event
            await PublishOrderCreatedEventAsync(order);

            _logger.LogInformation("Order {OrderId} created successfully for customer {CustomerId}", order.Id, order.CustomerId);
            return order;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error creating order for customer {CustomerId}", request.CustomerId);
            throw;
        }
    }

    public async Task<Order?> GetOrderByIdAsync(Guid id)
    {
        return await _context.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id);
    }

    public async Task<IEnumerable<Order>> GetOrdersAsync(int page = 1, int pageSize = 10)
    {
        return await _context.Orders
            .Include(o => o.Items)
            .OrderByDescending(o => o.OrderDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<IEnumerable<Order>> GetOrdersByCustomerAsync(string customerId, int page = 1, int pageSize = 10)
    {
        return await _context.Orders
            .Include(o => o.Items)
            .Where(o => o.CustomerId == customerId)
            .OrderByDescending(o => o.OrderDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<bool> UpdateOrderStatusAsync(Guid id, OrderStatus status, string? notes = null)
    {
        var order = await _context.Orders.FindAsync(id);
        if (order == null)
            return false;

        var oldStatus = order.Status;
        order.Status = status;
        order.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrEmpty(notes))
            order.Notes = notes;

        if (status == OrderStatus.Shipped)
            order.ShippedDate = DateTime.UtcNow;
        else if (status == OrderStatus.Delivered)
            order.DeliveredDate = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // Publish status change event
        await PublishOrderStatusChangedEventAsync(order, oldStatus, status);

        _logger.LogInformation("Order {OrderId} status changed from {OldStatus} to {NewStatus}", id, oldStatus, status);
        return true;
    }

    public async Task<bool> CancelOrderAsync(Guid id, string reason)
    {
        var order = await _context.Orders.FindAsync(id);
        if (order == null || order.Status == OrderStatus.Cancelled)
            return false;

        if (order.Status == OrderStatus.Shipped || order.Status == OrderStatus.Delivered)
            throw new InvalidOperationException("Cannot cancel shipped or delivered orders");

        order.Status = OrderStatus.Cancelled;
        order.Notes = $"Cancelled: {reason}";
        order.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // Publish cancellation event
        await PublishOrderCancelledEventAsync(order, reason);

        _logger.LogInformation("Order {OrderId} cancelled. Reason: {Reason}", id, reason);
        return true;
    }

    public async Task<decimal> GetOrderTotalAsync(Guid id)
    {
        var order = await _context.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id);

        return order?.TotalAmount ?? 0;
    }

    public async Task<IEnumerable<Order>> GetOrdersByStatusAsync(OrderStatus status, int page = 1, int pageSize = 10)
    {
        return await _context.Orders
            .Include(o => o.Items)
            .Where(o => o.Status == status)
            .OrderByDescending(o => o.OrderDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    private async Task<string> GetProductNameAsync(string productId)
    {
        // In a real implementation, this would call the catalog service
        // For now, return a placeholder
        await Task.Delay(1);
        return $"Product {productId}";
    }

    private async Task<(bool Success, List<string> UnavailableItems)> CheckInventoryAsync(IEnumerable<OrderItem> items)
    {
        // In a real implementation, this would call the inventory service
        // For now, simulate inventory check
        await Task.Delay(10);
        return (true, new List<string>());
    }

    private async Task PublishOrderCreatedEventAsync(Order order)
    {
        var orderEvent = new
        {
            EventType = "OrderCreated",
            OrderId = order.Id,
            CustomerId = order.CustomerId,
            TotalAmount = order.TotalAmount,
            Items = order.Items.Select(i => new
            {
                ProductId = i.ProductId,
                Quantity = i.Quantity,
                Price = i.Price
            }),
            Timestamp = DateTime.UtcNow
        };

        await _serviceBus.PublishEventAsync("order-events", orderEvent);
    }

    private async Task PublishOrderStatusChangedEventAsync(Order order, OrderStatus oldStatus, OrderStatus newStatus)
    {
        var statusEvent = new
        {
            EventType = "OrderStatusChanged",
            OrderId = order.Id,
            CustomerId = order.CustomerId,
            OldStatus = oldStatus.ToString(),
            NewStatus = newStatus.ToString(),
            Timestamp = DateTime.UtcNow
        };

        await _serviceBus.PublishEventAsync("order-events", statusEvent);
    }

    private async Task PublishOrderCancelledEventAsync(Order order, string reason)
    {
        var cancelEvent = new
        {
            EventType = "OrderCancelled",
            OrderId = order.Id,
            CustomerId = order.CustomerId,
            Reason = reason,
            Timestamp = DateTime.UtcNow
        };

        await _serviceBus.PublishEventAsync("order-events", cancelEvent);
    }
}