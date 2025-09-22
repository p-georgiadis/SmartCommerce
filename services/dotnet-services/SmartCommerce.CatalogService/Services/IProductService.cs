using SmartCommerce.CatalogService.Models;

namespace SmartCommerce.CatalogService.Services;

public interface IProductService
{
    // Product CRUD operations
    Task<Product> CreateProductAsync(CreateProductRequest request, string createdBy);
    Task<Product?> GetProductByIdAsync(string id);
    Task<Product?> GetProductBySkuAsync(string sku);
    Task<IEnumerable<Product>> GetProductsAsync(int page = 1, int pageSize = 20);
    Task<Product?> UpdateProductAsync(string id, UpdateProductRequest request, string updatedBy);
    Task<bool> DeleteProductAsync(string id);

    // Search and filtering
    Task<(IEnumerable<Product> Products, int TotalCount)> SearchProductsAsync(ProductSearchRequest request);
    Task<IEnumerable<Product>> GetProductsByCategoryAsync(string category, int page = 1, int pageSize = 20);
    Task<IEnumerable<Product>> GetProductsByBrandAsync(string brand, int page = 1, int pageSize = 20);
    Task<IEnumerable<Product>> GetFeaturedProductsAsync(int count = 10);
    Task<IEnumerable<Product>> GetRelatedProductsAsync(string productId, int count = 5);

    // Stock management
    Task<bool> UpdateStockAsync(string productId, int quantity, string reason = "");
    Task<bool> BulkUpdateStockAsync(BulkUpdateStockRequest request);
    Task<IEnumerable<Product>> GetLowStockProductsAsync(int? threshold = null);

    // Categories
    Task<IEnumerable<Category>> GetCategoriesAsync();
    Task<Category?> GetCategoryByIdAsync(string id);
    Task<Category> CreateCategoryAsync(Category category);
    Task<Category?> UpdateCategoryAsync(string id, Category category);
    Task<bool> DeleteCategoryAsync(string id);

    // Analytics and reporting
    Task<Dictionary<string, int>> GetCategoryStatsAsync();
    Task<IEnumerable<Product>> GetTopSellingProductsAsync(int count = 10, int days = 30);
    Task<IEnumerable<Product>> GetRecentlyAddedProductsAsync(int count = 10);

    // Import/Export
    Task<string> ImportProductsAsync(ProductImportRequest request);
    Task<string> ExportProductsAsync(string format = "csv", string? category = null);

    // Pricing
    Task<bool> UpdatePriceAsync(string productId, decimal price, decimal? compareAtPrice = null);
    Task<bool> BulkUpdatePricesAsync(Dictionary<string, decimal> priceUpdates);

    // SEO
    Task<bool> UpdateSeoAsync(string productId, ProductSeoRequest seo);
    Task<Product?> GetProductBySlugAsync(string slug);
}