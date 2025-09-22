using System.ComponentModel.DataAnnotations;

namespace SmartCommerce.CatalogService.Models;

public class CreateProductRequest
{
    [Required]
    [StringLength(500)]
    public string Name { get; set; } = string.Empty;

    [StringLength(2000)]
    public string Description { get; set; } = string.Empty;

    [StringLength(100)]
    public string Sku { get; set; } = string.Empty;

    [Required]
    public string Category { get; set; } = string.Empty;

    public string? SubCategory { get; set; }

    public string? Brand { get; set; }

    [Range(0.01, double.MaxValue)]
    public decimal Price { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? CompareAtPrice { get; set; }

    [Range(0, double.MaxValue)]
    public decimal Cost { get; set; }

    [Range(0, int.MaxValue)]
    public int StockQuantity { get; set; }

    [Range(0, int.MaxValue)]
    public int ReorderLevel { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsFeatured { get; set; }

    public bool IsDigital { get; set; }

    public double Weight { get; set; }

    public string WeightUnit { get; set; } = "kg";

    public string? Dimensions { get; set; }

    public List<string> Images { get; set; } = new();

    public List<string> Tags { get; set; } = new();

    public Dictionary<string, object> Attributes { get; set; } = new();

    public ProductSeoRequest? Seo { get; set; }
}

public class UpdateProductRequest
{
    [StringLength(500)]
    public string? Name { get; set; }

    [StringLength(2000)]
    public string? Description { get; set; }

    public string? Category { get; set; }

    public string? SubCategory { get; set; }

    public string? Brand { get; set; }

    [Range(0.01, double.MaxValue)]
    public decimal? Price { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? CompareAtPrice { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? Cost { get; set; }

    [Range(0, int.MaxValue)]
    public int? StockQuantity { get; set; }

    [Range(0, int.MaxValue)]
    public int? ReorderLevel { get; set; }

    public bool? IsActive { get; set; }

    public bool? IsFeatured { get; set; }

    public bool? IsDigital { get; set; }

    public double? Weight { get; set; }

    public string? WeightUnit { get; set; }

    public string? Dimensions { get; set; }

    public List<string>? Images { get; set; }

    public List<string>? Tags { get; set; }

    public Dictionary<string, object>? Attributes { get; set; }

    public ProductSeoRequest? Seo { get; set; }
}

public class ProductSeoRequest
{
    [StringLength(150)]
    public string MetaTitle { get; set; } = string.Empty;

    [StringLength(300)]
    public string MetaDescription { get; set; } = string.Empty;

    [StringLength(100)]
    public string Slug { get; set; } = string.Empty;

    public List<string> MetaKeywords { get; set; } = new();
}

public class ProductSearchRequest
{
    public string? Query { get; set; }

    public string? Category { get; set; }

    public string? Brand { get; set; }

    public decimal? MinPrice { get; set; }

    public decimal? MaxPrice { get; set; }

    public bool? InStock { get; set; }

    public bool? IsActive { get; set; }

    public bool? IsFeatured { get; set; }

    public List<string> Tags { get; set; } = new();

    public string SortBy { get; set; } = "name"; // name, price, created, rating

    public string SortOrder { get; set; } = "asc"; // asc, desc

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 20;
}

public class BulkUpdateStockRequest
{
    public List<StockUpdateItem> Items { get; set; } = new();
}

public class StockUpdateItem
{
    [Required]
    public string ProductId { get; set; } = string.Empty;

    [Range(0, int.MaxValue)]
    public int Quantity { get; set; }

    public string? Reason { get; set; }
}

public class ProductImportRequest
{
    [Required]
    public string FileUrl { get; set; } = string.Empty;

    public string Format { get; set; } = "csv"; // csv, json, xml

    public bool ValidateOnly { get; set; }

    public bool UpdateExisting { get; set; } = true;
}