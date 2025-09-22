using System.ComponentModel.DataAnnotations;

namespace SmartCommerce.OrderService.Models;

public class OrderItem
{
    public Guid Id { get; set; }

    public Guid OrderId { get; set; }

    [Required]
    public string ProductId { get; set; } = string.Empty;

    [Required]
    public string ProductName { get; set; } = string.Empty;

    [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1")]
    public int Quantity { get; set; }

    [Range(0.01, double.MaxValue, ErrorMessage = "Price must be greater than 0")]
    public decimal Price { get; set; }

    public decimal Discount { get; set; }

    public string? ProductSku { get; set; }

    public string? ProductImageUrl { get; set; }

    public Order Order { get; set; } = null!;
}