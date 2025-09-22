using SmartCommerce.OrderService.Models;

namespace SmartCommerce.OrderService.Services;

public interface IOrderService
{
    Task<Order> CreateOrderAsync(CreateOrderRequest request);
    Task<Order?> GetOrderByIdAsync(Guid id);
    Task<IEnumerable<Order>> GetOrdersAsync(int page = 1, int pageSize = 10);
    Task<IEnumerable<Order>> GetOrdersByCustomerAsync(string customerId, int page = 1, int pageSize = 10);
    Task<bool> UpdateOrderStatusAsync(Guid id, OrderStatus status, string? notes = null);
    Task<bool> CancelOrderAsync(Guid id, string reason);
    Task<decimal> GetOrderTotalAsync(Guid id);
    Task<IEnumerable<Order>> GetOrdersByStatusAsync(OrderStatus status, int page = 1, int pageSize = 10);
}