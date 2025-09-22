using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartCommerce.CatalogService.Models;
using SmartCommerce.CatalogService.Services;
using System.Security.Claims;

namespace SmartCommerce.CatalogService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductController : ControllerBase
{
    private readonly IProductService _productService;
    private readonly ILogger<ProductController> _logger;

    public ProductController(IProductService productService, ILogger<ProductController> logger)
    {
        _productService = productService;
        _logger = logger;
    }

    /// <summary>
    /// Get all products with pagination
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Product>>> GetProducts(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (page < 1 || pageSize < 1 || pageSize > 100)
            return BadRequest("Invalid pagination parameters");

        var products = await _productService.GetProductsAsync(page, pageSize);
        return Ok(products);
    }

    /// <summary>
    /// Get product by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<Product>> GetProduct(string id)
    {
        var product = await _productService.GetProductByIdAsync(id);
        if (product == null)
            return NotFound($"Product with ID {id} not found");

        return Ok(product);
    }

    /// <summary>
    /// Get product by SKU
    /// </summary>
    [HttpGet("sku/{sku}")]
    public async Task<ActionResult<Product>> GetProductBySku(string sku)
    {
        var product = await _productService.GetProductBySkuAsync(sku);
        if (product == null)
            return NotFound($"Product with SKU {sku} not found");

        return Ok(product);
    }

    /// <summary>
    /// Get product by SEO slug
    /// </summary>
    [HttpGet("slug/{slug}")]
    public async Task<ActionResult<Product>> GetProductBySlug(string slug)
    {
        var product = await _productService.GetProductBySlugAsync(slug);
        if (product == null)
            return NotFound($"Product with slug {slug} not found");

        return Ok(product);
    }

    /// <summary>
    /// Search products with filters
    /// </summary>
    [HttpPost("search")]
    public async Task<ActionResult> SearchProducts([FromBody] ProductSearchRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var (products, totalCount) = await _productService.SearchProductsAsync(request);

        var response = new
        {
            Products = products,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize,
            TotalPages = (int)Math.Ceiling((double)totalCount / request.PageSize)
        };

        return Ok(response);
    }

    /// <summary>
    /// Get products by category
    /// </summary>
    [HttpGet("category/{category}")]
    public async Task<ActionResult<IEnumerable<Product>>> GetProductsByCategory(
        string category,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var products = await _productService.GetProductsByCategoryAsync(category, page, pageSize);
        return Ok(products);
    }

    /// <summary>
    /// Get products by brand
    /// </summary>
    [HttpGet("brand/{brand}")]
    public async Task<ActionResult<IEnumerable<Product>>> GetProductsByBrand(
        string brand,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var products = await _productService.GetProductsByBrandAsync(brand, page, pageSize);
        return Ok(products);
    }

    /// <summary>
    /// Get featured products
    /// </summary>
    [HttpGet("featured")]
    public async Task<ActionResult<IEnumerable<Product>>> GetFeaturedProducts([FromQuery] int count = 10)
    {
        var products = await _productService.GetFeaturedProductsAsync(count);
        return Ok(products);
    }

    /// <summary>
    /// Get related products
    /// </summary>
    [HttpGet("{id}/related")]
    public async Task<ActionResult<IEnumerable<Product>>> GetRelatedProducts(string id, [FromQuery] int count = 5)
    {
        var products = await _productService.GetRelatedProductsAsync(id, count);
        return Ok(products);
    }

    /// <summary>
    /// Get recently added products
    /// </summary>
    [HttpGet("recent")]
    public async Task<ActionResult<IEnumerable<Product>>> GetRecentlyAddedProducts([FromQuery] int count = 10)
    {
        var products = await _productService.GetRecentlyAddedProductsAsync(count);
        return Ok(products);
    }

    /// <summary>
    /// Create a new product
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<ActionResult<Product>> CreateProduct([FromBody] CreateProductRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var createdBy = GetCurrentUserId() ?? "system";
            var product = await _productService.CreateProductAsync(request, createdBy);
            return CreatedAtAction(nameof(GetProduct), new { id = product.Id }, product);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating product");
            return StatusCode(500, "An error occurred while creating the product");
        }
    }

    /// <summary>
    /// Update a product
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<ActionResult<Product>> UpdateProduct(string id, [FromBody] UpdateProductRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var updatedBy = GetCurrentUserId() ?? "system";
            var product = await _productService.UpdateProductAsync(id, request, updatedBy);
            if (product == null)
                return NotFound($"Product with ID {id} not found");

            return Ok(product);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating product {ProductId}", id);
            return StatusCode(500, "An error occurred while updating the product");
        }
    }

    /// <summary>
    /// Delete a product
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> DeleteProduct(string id)
    {
        try
        {
            var result = await _productService.DeleteProductAsync(id);
            if (!result)
                return NotFound($"Product with ID {id} not found");

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting product {ProductId}", id);
            return StatusCode(500, "An error occurred while deleting the product");
        }
    }

    /// <summary>
    /// Update product stock
    /// </summary>
    [HttpPut("{id}/stock")]
    [Authorize(Roles = "Admin,Manager,Inventory")]
    public async Task<ActionResult> UpdateStock(string id, [FromBody] UpdateStockRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var result = await _productService.UpdateStockAsync(id, request.Quantity, request.Reason);
            if (!result)
                return NotFound($"Product with ID {id} not found");

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating stock for product {ProductId}", id);
            return StatusCode(500, "An error occurred while updating stock");
        }
    }

    /// <summary>
    /// Bulk update stock for multiple products
    /// </summary>
    [HttpPut("stock/bulk")]
    [Authorize(Roles = "Admin,Manager,Inventory")]
    public async Task<ActionResult> BulkUpdateStock([FromBody] BulkUpdateStockRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            await _productService.BulkUpdateStockAsync(request);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing bulk stock update");
            return StatusCode(500, "An error occurred while updating stock");
        }
    }

    /// <summary>
    /// Get low stock products
    /// </summary>
    [HttpGet("low-stock")]
    [Authorize(Roles = "Admin,Manager,Inventory")]
    public async Task<ActionResult<IEnumerable<Product>>> GetLowStockProducts([FromQuery] int? threshold = null)
    {
        var products = await _productService.GetLowStockProductsAsync(threshold);
        return Ok(products);
    }

    /// <summary>
    /// Update product price
    /// </summary>
    [HttpPut("{id}/price")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<ActionResult> UpdatePrice(string id, [FromBody] UpdatePriceRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var result = await _productService.UpdatePriceAsync(id, request.Price, request.CompareAtPrice);
            if (!result)
                return NotFound($"Product with ID {id} not found");

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating price for product {ProductId}", id);
            return StatusCode(500, "An error occurred while updating price");
        }
    }

    /// <summary>
    /// Update product SEO
    /// </summary>
    [HttpPut("{id}/seo")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<ActionResult> UpdateSeo(string id, [FromBody] ProductSeoRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var result = await _productService.UpdateSeoAsync(id, request);
            if (!result)
                return NotFound($"Product with ID {id} not found");

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating SEO for product {ProductId}", id);
            return StatusCode(500, "An error occurred while updating SEO");
        }
    }

    /// <summary>
    /// Get category statistics
    /// </summary>
    [HttpGet("stats/categories")]
    public async Task<ActionResult<Dictionary<string, int>>> GetCategoryStats()
    {
        var stats = await _productService.GetCategoryStatsAsync();
        return Ok(stats);
    }

    /// <summary>
    /// Import products from file
    /// </summary>
    [HttpPost("import")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<ActionResult> ImportProducts([FromBody] ProductImportRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var result = await _productService.ImportProductsAsync(request);
            return Ok(new { Message = result });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing products");
            return StatusCode(500, "An error occurred while importing products");
        }
    }

    /// <summary>
    /// Export products to file
    /// </summary>
    [HttpPost("export")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<ActionResult> ExportProducts(
        [FromQuery] string format = "csv",
        [FromQuery] string? category = null)
    {
        try
        {
            var result = await _productService.ExportProductsAsync(format, category);
            return Ok(new { Message = result });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting products");
            return StatusCode(500, "An error occurred while exporting products");
        }
    }

    private string? GetCurrentUserId()
    {
        return User.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
               User.FindFirst("sub")?.Value;
    }
}

public class UpdateStockRequest
{
    public int Quantity { get; set; }
    public string? Reason { get; set; }
}

public class UpdatePriceRequest
{
    public decimal Price { get; set; }
    public decimal? CompareAtPrice { get; set; }
}