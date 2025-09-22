using System.ComponentModel.DataAnnotations;

namespace SmartCommerce.OrderService.Models;

public class CreateOrderRequest
{
    [Required]
    public string CustomerId { get; set; } = string.Empty;

    [Required]
    [MinLength(1, ErrorMessage = "At least one item is required")]
    public List<CreateOrderItemRequest> Items { get; set; } = new();

    public string? Notes { get; set; }
}

public class CreateOrderItemRequest
{
    [Required]
    public string ProductId { get; set; } = string.Empty;

    [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1")]
    public int Quantity { get; set; }

    [Range(0.01, double.MaxValue, ErrorMessage = "Price must be greater than 0")]
    public decimal Price { get; set; }

    public decimal Discount { get; set; }
}

public class UpdateOrderStatusRequest
{
    [Required]
    public OrderStatus Status { get; set; }

    public string? Notes { get; set; }
}