using Microsoft.AspNetCore.Mvc;
using Product.API.Application.DTOs;
using Product.API.Application.Services;
using Product.API.Application.Interfaces;

namespace Product.API.API.Controllers;

[ApiController]
[Route("api/products")]
public class ProductController : ControllerBase
{
    private readonly IProductService _productService;
    private readonly ILogger<ProductController> _logger;

    public ProductController(IProductService productService, ILogger<ProductController> logger)
    {
        _productService = productService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetProducts(
        [FromQuery] string? category, 
        [FromQuery] string? search)
    {
        if (!string.IsNullOrEmpty(search))
        {
            var searchResults = await _productService.SearchProductsAsync(search);
            return Ok(searchResults);
        }
        if (!string.IsNullOrEmpty(category))
        {
            var categoryResults = await _productService.GetProductsByCategoryAsync(category);
            return Ok(categoryResults);
        }
        var products = await _productService.GetProductsAsync();
        return Ok(products);
    }
   
    [HttpGet("{id}")]
    public async Task<IActionResult> GetProduct(string id)
    {
        var product = await _productService.GetProductByIdAsync(id);
        if (product == null) return NotFound(new { message = $"Product {id} not found" });
        return Ok(product);
    }

    [HttpGet("category/{category}")]
    public async Task<IActionResult> GetByCategory(string category)
    {
        var products = await _productService.GetProductsByCategoryAsync(category);
        return Ok(products);
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string q)
    {
        if (string.IsNullOrEmpty(q)) return BadRequest("Search query is required");
        var products = await _productService.SearchProductsAsync(q);
        return Ok(products);
    }

    [HttpPost("batch")]
    public async Task<IActionResult> GetByIds([FromBody] List<string> ids)
    {
        var products = await _productService.GetProductsByIdsAsync(ids);
        return Ok(products);
    }

    [HttpPost]
    public async Task<IActionResult> CreateProduct([FromBody] CreateProductDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var product = await _productService.CreateProductAsync(dto);
        return CreatedAtAction(nameof(GetProduct), new { id = product.Id }, product);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateProduct(string id, [FromBody] UpdateProductDto dto)
    {
        var result = await _productService.UpdateProductAsync(id, dto);
        if (!result) return NotFound(new { message = $"Product {id} not found" });
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteProduct(string id)
    {
        var result = await _productService.DeleteProductAsync(id);
        if (!result) return NotFound(new { message = $"Product {id} not found" });
        return NoContent();
    }

    [HttpPost("{id}/decrement-stock")]
    public async Task<IActionResult> DecrementStock(string id, [FromBody] DecrementStockDto dto)
    {
        try
        {
            var result = await _productService.DecrementStockAsync(id, dto.Quantity);
            if (!result) return NotFound(new { message = $"Product {id} not found" });
            return Ok(new { message = "Stock updated" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id}/view")]
    public async Task<IActionResult> RecordView(string id, [FromBody] RecordViewDto dto)
    {
        await _productService.RecordViewAsync(id, dto.UserId);
        return Ok();
    }

    [HttpGet("paged")]
    public async Task<IActionResult> GetPaged(
    int page = 1,
    int pageSize = 10)
    {
        var result = await _productService.GetPagedAsync(page, pageSize);
        return Ok(result);
    }
}

[ApiController]
[Route("api/categories")]
public class CategoryController : ControllerBase
{
    private readonly Application.Interfaces.ICategoryRepository _categoryRepository;

    public CategoryController(Application.Interfaces.ICategoryRepository categoryRepository)
    {
        _categoryRepository = categoryRepository;
    }

    [HttpGet]
    public async Task<IActionResult> GetCategories()
    {
        var categories = await _categoryRepository.GetAllAsync();
        return Ok(categories);
    }
    
    [HttpGet("{id}")]
    public async Task<IActionResult> GetCategory(string id)
    {
        var category = await _categoryRepository.GetByIdAsync(id);
        if (category == null) return NotFound();
        return Ok(category);
    }

    [HttpPost]
    public async Task<IActionResult> CreateCategory([FromBody] Domain.Entities.Category category)
    {
        var created = await _categoryRepository.CreateAsync(category);
        return CreatedAtAction(nameof(GetCategory), new { id = created.Id }, created);
    }
}
