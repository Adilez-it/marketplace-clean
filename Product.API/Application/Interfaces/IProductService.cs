using Product.API.Application.DTOs;

namespace Product.API.Application.Interfaces;

public interface IProductService
{
    Task<List<ProductDto>> GetProductsAsync();
    Task<ProductDto?> GetProductByIdAsync(string id);
    Task<List<ProductDto>> GetProductsByCategoryAsync(string category);
    Task<List<ProductDto>> SearchProductsAsync(string query);
    Task<List<ProductDto>> GetProductsByIdsAsync(IEnumerable<string> ids);
    Task<ProductDto> CreateProductAsync(CreateProductDto dto);
    Task<ProductDto> CreateOrUpdateProductAsync(CreateProductDto dto);
    Task<bool> UpdateProductAsync(string id, UpdateProductDto dto);
    Task<bool> DeleteProductAsync(string id);
    Task<bool> DecrementStockAsync(string id, int quantity);
    Task RecordViewAsync(string productId, string userId);
    Task<PagedResult<ProductDto>> GetPagedAsync(int page, int pageSize);
}