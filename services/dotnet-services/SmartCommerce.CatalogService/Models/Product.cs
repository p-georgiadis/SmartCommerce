using System.ComponentModel.DataAnnotations;

namespace SmartCommerce.CatalogService.Models;

public class Product
{
    public string Id { get; set; } = string.Empty;

    [Required]
    [StringLength(500)]
    public string Name { get; set; } = string.Empty;

    [StringLength(2000)]
    public string Description { get; set; } = string.Empty;

    [StringLength(100)]
    public string Sku { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string Category { get; set; } = string.Empty;

    [StringLength(100)]
    public string SubCategory { get; set; } = string.Empty;

    [StringLength(100)]
    public string Brand { get; set; } = string.Empty;

    [Range(0, double.MaxValue)]
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

    [StringLength(50)]
    public string WeightUnit { get; set; } = "kg";

    public string Dimensions { get; set; } = string.Empty;

    public List<string> Images { get; set; } = new();

    public List<string> Tags { get; set; } = new();

    public Dictionary<string, object> Attributes { get; set; } = new();

    public ProductSeo? Seo { get; set; }

    public ProductRating Rating { get; set; } = new();

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    [StringLength(100)]
    public string CreatedBy { get; set; } = string.Empty;

    [StringLength(100)]
    public string UpdatedBy { get; set; } = string.Empty;
}

public class ProductSeo
{
    [StringLength(150)]
    public string MetaTitle { get; set; } = string.Empty;

    [StringLength(300)]
    public string MetaDescription { get; set; } = string.Empty;

    [StringLength(100)]
    public string Slug { get; set; } = string.Empty;

    public List<string> MetaKeywords { get; set; } = new();
}

public class ProductRating
{
    public double AverageRating { get; set; }

    public int TotalRatings { get; set; }

    public Dictionary<int, int> RatingDistribution { get; set; } = new()
    {
        { 1, 0 }, { 2, 0 }, { 3, 0 }, { 4, 0 }, { 5, 0 }
    };
}

public class ProductVariant
{
    public string Id { get; set; } = string.Empty;

    public string ProductId { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(100)]
    public string Sku { get; set; } = string.Empty;

    [Range(0, double.MaxValue)]
    public decimal Price { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? CompareAtPrice { get; set; }

    [Range(0, int.MaxValue)]
    public int StockQuantity { get; set; }

    public bool IsActive { get; set; } = true;

    public Dictionary<string, string> Options { get; set; } = new(); // Size: Large, Color: Red

    public List<string> Images { get; set; } = new();

    public Product Product { get; set; } = null!;
}

public class Category
{
    public string Id { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string Description { get; set; } = string.Empty;

    public string? ParentCategoryId { get; set; }

    [StringLength(100)]
    public string Slug { get; set; } = string.Empty;

    public string? ImageUrl { get; set; }

    public bool IsActive { get; set; } = true;

    public int SortOrder { get; set; }

    public Category? ParentCategory { get; set; }

    public List<Category> SubCategories { get; set; } = new();

    public List<Product> Products { get; set; } = new();

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}