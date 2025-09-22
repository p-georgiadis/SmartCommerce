using Microsoft.EntityFrameworkCore;
using SmartCommerce.CatalogService.Data;
using SmartCommerce.CatalogService.Models;
using SmartCommerce.Shared.Messaging;
using StackExchange.Redis;
using System.Text.Json;

namespace SmartCommerce.CatalogService.Services;

public class ProductService : IProductService
{
    private readonly CatalogDbContext _context;
    private readonly ILogger<ProductService> _logger;
    private readonly IServiceBusClient _serviceBus;
    private readonly IDatabase? _cache;

    public ProductService(
        CatalogDbContext context,
        ILogger<ProductService> logger,
        IServiceBusClient serviceBus,
        IConnectionMultiplexer? redis = null)
    {
        _context = context;
        _logger = logger;
        _serviceBus = serviceBus;
        _cache = redis?.GetDatabase();
    }

    public async Task<Product> CreateProductAsync(CreateProductRequest request, string createdBy)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            // Generate product ID
            var productId = await GenerateProductIdAsync();

            var product = new Product
            {
                Id = productId,
                Name = request.Name,
                Description = request.Description,
                Sku = string.IsNullOrEmpty(request.Sku) ? await GenerateSkuAsync() : request.Sku,
                Category = request.Category,
                SubCategory = request.SubCategory,
                Brand = request.Brand,
                Price = request.Price,
                CompareAtPrice = request.CompareAtPrice,
                Cost = request.Cost,
                StockQuantity = request.StockQuantity,
                ReorderLevel = request.ReorderLevel,
                IsActive = request.IsActive,
                IsFeatured = request.IsFeatured,
                IsDigital = request.IsDigital,
                Weight = request.Weight,
                WeightUnit = request.WeightUnit,
                Dimensions = request.Dimensions ?? string.Empty,
                Images = request.Images,
                Tags = request.Tags,
                Attributes = request.Attributes,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CreatedBy = createdBy,
                UpdatedBy = createdBy
            };

            // Set SEO if provided
            if (request.Seo != null)
            {
                product.Seo = new ProductSeo
                {
                    MetaTitle = request.Seo.MetaTitle,
                    MetaDescription = request.Seo.MetaDescription,
                    Slug = string.IsNullOrEmpty(request.Seo.Slug) ? GenerateSlug(request.Name) : request.Seo.Slug,
                    MetaKeywords = request.Seo.MetaKeywords
                };
            }
            else
            {
                product.Seo = new ProductSeo
                {
                    Slug = GenerateSlug(request.Name)
                };
            }

            _context.Products.Add(product);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            // Invalidate cache
            await InvalidateProductCacheAsync();

            // Publish product created event
            await PublishProductCreatedEventAsync(product);

            _logger.LogInformation("Product {ProductId} created successfully", product.Id);
            return product;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error creating product");
            throw;
        }
    }

    public async Task<Product?> GetProductByIdAsync(string id)
    {
        var cacheKey = $"product:{id}";

        // Try cache first
        if (_cache != null)
        {
            var cached = await _cache.StringGetAsync(cacheKey);
            if (cached.HasValue)
            {
                return JsonSerializer.Deserialize<Product>(cached!);
            }
        }

        var product = await _context.Products
            .FirstOrDefaultAsync(p => p.Id == id);

        // Cache the result
        if (product != null && _cache != null)
        {
            await _cache.StringSetAsync(cacheKey, JsonSerializer.Serialize(product), TimeSpan.FromMinutes(15));
        }

        return product;
    }

    public async Task<Product?> GetProductBySkuAsync(string sku)
    {
        var cacheKey = $"product:sku:{sku}";

        if (_cache != null)
        {
            var cached = await _cache.StringGetAsync(cacheKey);
            if (cached.HasValue)
            {
                return JsonSerializer.Deserialize<Product>(cached!);
            }
        }

        var product = await _context.Products
            .FirstOrDefaultAsync(p => p.Sku == sku);

        if (product != null && _cache != null)
        {
            await _cache.StringSetAsync(cacheKey, JsonSerializer.Serialize(product), TimeSpan.FromMinutes(15));
        }

        return product;
    }

    public async Task<IEnumerable<Product>> GetProductsAsync(int page = 1, int pageSize = 20)
    {
        return await _context.Products
            .Where(p => p.IsActive)
            .OrderBy(p => p.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<Product?> UpdateProductAsync(string id, UpdateProductRequest request, string updatedBy)
    {
        var product = await _context.Products.FindAsync(id);
        if (product == null)
            return null;

        // Update only provided fields
        if (request.Name != null) product.Name = request.Name;
        if (request.Description != null) product.Description = request.Description;
        if (request.Category != null) product.Category = request.Category;
        if (request.SubCategory != null) product.SubCategory = request.SubCategory;
        if (request.Brand != null) product.Brand = request.Brand;
        if (request.Price.HasValue) product.Price = request.Price.Value;
        if (request.CompareAtPrice.HasValue) product.CompareAtPrice = request.CompareAtPrice;
        if (request.Cost.HasValue) product.Cost = request.Cost.Value;
        if (request.StockQuantity.HasValue) product.StockQuantity = request.StockQuantity.Value;
        if (request.ReorderLevel.HasValue) product.ReorderLevel = request.ReorderLevel.Value;
        if (request.IsActive.HasValue) product.IsActive = request.IsActive.Value;
        if (request.IsFeatured.HasValue) product.IsFeatured = request.IsFeatured.Value;
        if (request.IsDigital.HasValue) product.IsDigital = request.IsDigital.Value;
        if (request.Weight.HasValue) product.Weight = request.Weight.Value;
        if (request.WeightUnit != null) product.WeightUnit = request.WeightUnit;
        if (request.Dimensions != null) product.Dimensions = request.Dimensions;
        if (request.Images != null) product.Images = request.Images;
        if (request.Tags != null) product.Tags = request.Tags;
        if (request.Attributes != null) product.Attributes = request.Attributes;

        product.UpdatedBy = updatedBy;
        product.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // Invalidate cache
        await InvalidateProductCacheAsync(id);

        // Publish product updated event
        await PublishProductUpdatedEventAsync(product);

        _logger.LogInformation("Product {ProductId} updated successfully", id);
        return product;
    }

    public async Task<bool> DeleteProductAsync(string id)
    {
        var product = await _context.Products.FindAsync(id);
        if (product == null)
            return false;

        _context.Products.Remove(product);
        await _context.SaveChangesAsync();

        // Invalidate cache
        await InvalidateProductCacheAsync(id);

        _logger.LogInformation("Product {ProductId} deleted successfully", id);
        return true;
    }

    public async Task<(IEnumerable<Product> Products, int TotalCount)> SearchProductsAsync(ProductSearchRequest request)
    {
        var query = _context.Products.AsQueryable();

        // Apply filters
        if (!string.IsNullOrEmpty(request.Query))
        {
            query = query.Where(p => p.Name.Contains(request.Query) ||
                                   p.Description.Contains(request.Query) ||
                                   p.Tags.Any(t => t.Contains(request.Query)));
        }

        if (!string.IsNullOrEmpty(request.Category))
        {
            query = query.Where(p => p.Category == request.Category);
        }

        if (!string.IsNullOrEmpty(request.Brand))
        {
            query = query.Where(p => p.Brand == request.Brand);
        }

        if (request.MinPrice.HasValue)
        {
            query = query.Where(p => p.Price >= request.MinPrice.Value);
        }

        if (request.MaxPrice.HasValue)
        {
            query = query.Where(p => p.Price <= request.MaxPrice.Value);
        }

        if (request.InStock.HasValue)
        {
            query = request.InStock.Value
                ? query.Where(p => p.StockQuantity > 0)
                : query.Where(p => p.StockQuantity == 0);
        }

        if (request.IsActive.HasValue)
        {
            query = query.Where(p => p.IsActive == request.IsActive.Value);
        }

        if (request.IsFeatured.HasValue)
        {
            query = query.Where(p => p.IsFeatured == request.IsFeatured.Value);
        }

        if (request.Tags.Any())
        {
            query = query.Where(p => request.Tags.Any(tag => p.Tags.Contains(tag)));
        }

        var totalCount = await query.CountAsync();

        // Apply sorting
        query = request.SortBy.ToLower() switch
        {
            "price" => request.SortOrder.ToLower() == "desc"
                ? query.OrderByDescending(p => p.Price)
                : query.OrderBy(p => p.Price),
            "created" => request.SortOrder.ToLower() == "desc"
                ? query.OrderByDescending(p => p.CreatedAt)
                : query.OrderBy(p => p.CreatedAt),
            "rating" => request.SortOrder.ToLower() == "desc"
                ? query.OrderByDescending(p => p.Rating.AverageRating)
                : query.OrderBy(p => p.Rating.AverageRating),
            _ => request.SortOrder.ToLower() == "desc"
                ? query.OrderByDescending(p => p.Name)
                : query.OrderBy(p => p.Name)
        };

        var products = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        return (products, totalCount);
    }

    public async Task<IEnumerable<Product>> GetProductsByCategoryAsync(string category, int page = 1, int pageSize = 20)
    {
        return await _context.Products
            .Where(p => p.Category == category && p.IsActive)
            .OrderBy(p => p.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<IEnumerable<Product>> GetProductsByBrandAsync(string brand, int page = 1, int pageSize = 20)
    {
        return await _context.Products
            .Where(p => p.Brand == brand && p.IsActive)
            .OrderBy(p => p.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<IEnumerable<Product>> GetFeaturedProductsAsync(int count = 10)
    {
        var cacheKey = $"featured_products:{count}";

        if (_cache != null)
        {
            var cached = await _cache.StringGetAsync(cacheKey);
            if (cached.HasValue)
            {
                return JsonSerializer.Deserialize<IEnumerable<Product>>(cached!) ?? Enumerable.Empty<Product>();
            }
        }

        var products = await _context.Products
            .Where(p => p.IsFeatured && p.IsActive)
            .OrderByDescending(p => p.CreatedAt)
            .Take(count)
            .ToListAsync();

        if (_cache != null)
        {
            await _cache.StringSetAsync(cacheKey, JsonSerializer.Serialize(products), TimeSpan.FromMinutes(30));
        }

        return products;
    }

    public async Task<IEnumerable<Product>> GetRelatedProductsAsync(string productId, int count = 5)
    {
        var product = await GetProductByIdAsync(productId);
        if (product == null)
            return Enumerable.Empty<Product>();

        return await _context.Products
            .Where(p => p.Category == product.Category && p.Id != productId && p.IsActive)
            .OrderBy(p => Guid.NewGuid()) // Random order
            .Take(count)
            .ToListAsync();
    }

    public async Task<bool> UpdateStockAsync(string productId, int quantity, string reason = "")
    {
        var product = await _context.Products.FindAsync(productId);
        if (product == null)
            return false;

        var oldQuantity = product.StockQuantity;
        product.StockQuantity = quantity;
        product.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // Invalidate cache
        await InvalidateProductCacheAsync(productId);

        _logger.LogInformation("Stock updated for product {ProductId}: {OldQuantity} -> {NewQuantity}. Reason: {Reason}",
            productId, oldQuantity, quantity, reason);

        return true;
    }

    public async Task<bool> BulkUpdateStockAsync(BulkUpdateStockRequest request)
    {
        var productIds = request.Items.Select(i => i.ProductId).ToList();
        var products = await _context.Products
            .Where(p => productIds.Contains(p.Id))
            .ToListAsync();

        foreach (var item in request.Items)
        {
            var product = products.FirstOrDefault(p => p.Id == item.ProductId);
            if (product != null)
            {
                product.StockQuantity = item.Quantity;
                product.UpdatedAt = DateTime.UtcNow;
            }
        }

        await _context.SaveChangesAsync();

        // Invalidate cache
        await InvalidateProductCacheAsync();

        _logger.LogInformation("Bulk stock update completed for {Count} products", request.Items.Count);
        return true;
    }

    public async Task<IEnumerable<Product>> GetLowStockProductsAsync(int? threshold = null)
    {
        var defaultThreshold = threshold ?? 10;

        return await _context.Products
            .Where(p => p.StockQuantity <= p.ReorderLevel || p.StockQuantity <= defaultThreshold)
            .Where(p => p.IsActive)
            .OrderBy(p => p.StockQuantity)
            .ToListAsync();
    }

    public async Task<IEnumerable<Category>> GetCategoriesAsync()
    {
        return await _context.Categories
            .Where(c => c.IsActive)
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .ToListAsync();
    }

    public async Task<Category?> GetCategoryByIdAsync(string id)
    {
        return await _context.Categories
            .Include(c => c.SubCategories)
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<Category> CreateCategoryAsync(Category category)
    {
        category.Id = string.IsNullOrEmpty(category.Id) ? Guid.NewGuid().ToString() : category.Id;
        category.CreatedAt = DateTime.UtcNow;
        category.UpdatedAt = DateTime.UtcNow;

        _context.Categories.Add(category);
        await _context.SaveChangesAsync();

        return category;
    }

    public async Task<Category?> UpdateCategoryAsync(string id, Category category)
    {
        var existingCategory = await _context.Categories.FindAsync(id);
        if (existingCategory == null)
            return null;

        existingCategory.Name = category.Name;
        existingCategory.Description = category.Description;
        existingCategory.ParentCategoryId = category.ParentCategoryId;
        existingCategory.Slug = category.Slug;
        existingCategory.ImageUrl = category.ImageUrl;
        existingCategory.IsActive = category.IsActive;
        existingCategory.SortOrder = category.SortOrder;
        existingCategory.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return existingCategory;
    }

    public async Task<bool> DeleteCategoryAsync(string id)
    {
        var category = await _context.Categories.FindAsync(id);
        if (category == null)
            return false;

        _context.Categories.Remove(category);
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<Dictionary<string, int>> GetCategoryStatsAsync()
    {
        return await _context.Products
            .Where(p => p.IsActive)
            .GroupBy(p => p.Category)
            .ToDictionaryAsync(g => g.Key, g => g.Count());
    }

    public async Task<IEnumerable<Product>> GetTopSellingProductsAsync(int count = 10, int days = 30)
    {
        // This would typically join with order data
        // For now, return featured products as a placeholder
        return await GetFeaturedProductsAsync(count);
    }

    public async Task<IEnumerable<Product>> GetRecentlyAddedProductsAsync(int count = 10)
    {
        return await _context.Products
            .Where(p => p.IsActive)
            .OrderByDescending(p => p.CreatedAt)
            .Take(count)
            .ToListAsync();
    }

    public async Task<string> ImportProductsAsync(ProductImportRequest request)
    {
        // Placeholder implementation
        _logger.LogInformation("Product import requested from {FileUrl}", request.FileUrl);
        return "Import job created successfully";
    }

    public async Task<string> ExportProductsAsync(string format = "csv", string? category = null)
    {
        // Placeholder implementation
        _logger.LogInformation("Product export requested in {Format} format for category {Category}", format, category);
        return "Export job created successfully";
    }

    public async Task<bool> UpdatePriceAsync(string productId, decimal price, decimal? compareAtPrice = null)
    {
        var product = await _context.Products.FindAsync(productId);
        if (product == null)
            return false;

        product.Price = price;
        if (compareAtPrice.HasValue)
            product.CompareAtPrice = compareAtPrice.Value;
        product.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // Invalidate cache
        await InvalidateProductCacheAsync(productId);

        return true;
    }

    public async Task<bool> BulkUpdatePricesAsync(Dictionary<string, decimal> priceUpdates)
    {
        var productIds = priceUpdates.Keys.ToList();
        var products = await _context.Products
            .Where(p => productIds.Contains(p.Id))
            .ToListAsync();

        foreach (var product in products)
        {
            if (priceUpdates.TryGetValue(product.Id, out var newPrice))
            {
                product.Price = newPrice;
                product.UpdatedAt = DateTime.UtcNow;
            }
        }

        await _context.SaveChangesAsync();

        // Invalidate cache
        await InvalidateProductCacheAsync();

        return true;
    }

    public async Task<bool> UpdateSeoAsync(string productId, ProductSeoRequest seo)
    {
        var product = await _context.Products.FindAsync(productId);
        if (product == null)
            return false;

        product.Seo ??= new ProductSeo();
        product.Seo.MetaTitle = seo.MetaTitle;
        product.Seo.MetaDescription = seo.MetaDescription;
        product.Seo.Slug = seo.Slug;
        product.Seo.MetaKeywords = seo.MetaKeywords;
        product.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // Invalidate cache
        await InvalidateProductCacheAsync(productId);

        return true;
    }

    public async Task<Product?> GetProductBySlugAsync(string slug)
    {
        return await _context.Products
            .FirstOrDefaultAsync(p => p.Seo != null && p.Seo.Slug == slug && p.IsActive);
    }

    // Private helper methods
    private async Task<string> GenerateProductIdAsync()
    {
        string productId;
        do
        {
            productId = $"prod_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}_{Random.Shared.Next(1000, 9999)}";
        }
        while (await _context.Products.AnyAsync(p => p.Id == productId));

        return productId;
    }

    private async Task<string> GenerateSkuAsync()
    {
        string sku;
        do
        {
            sku = $"SKU{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}{Random.Shared.Next(100, 999)}";
        }
        while (await _context.Products.AnyAsync(p => p.Sku == sku));

        return sku;
    }

    private static string GenerateSlug(string name)
    {
        return name.ToLowerInvariant()
                   .Replace(" ", "-")
                   .Replace("_", "-")
                   .Trim('-');
    }

    private async Task InvalidateProductCacheAsync(string? productId = null)
    {
        if (_cache == null) return;

        try
        {
            if (!string.IsNullOrEmpty(productId))
            {
                await _cache.KeyDeleteAsync($"product:{productId}");
            }

            // Invalidate list caches
            await _cache.KeyDeleteAsync("featured_products:*");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to invalidate cache");
        }
    }

    private async Task PublishProductCreatedEventAsync(Product product)
    {
        var productEvent = new
        {
            EventType = "ProductCreated",
            ProductId = product.Id,
            Name = product.Name,
            Category = product.Category,
            Price = product.Price,
            Timestamp = DateTime.UtcNow
        };

        await _serviceBus.PublishEventAsync("product-events", productEvent);
    }

    private async Task PublishProductUpdatedEventAsync(Product product)
    {
        var productEvent = new
        {
            EventType = "ProductUpdated",
            ProductId = product.Id,
            Name = product.Name,
            Category = product.Category,
            Price = product.Price,
            Timestamp = DateTime.UtcNow
        };

        await _serviceBus.PublishEventAsync("product-events", productEvent);
    }
}