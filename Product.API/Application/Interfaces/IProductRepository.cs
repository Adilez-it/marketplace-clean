using Product.API.Domain.Entities;
using ProductEntity = Product.API.Domain.Entities.Product;

namespace Product.API.Application.Interfaces;

public interface IProductRepository
{
    Task<List<ProductEntity>> GetAllAsync();
    Task<ProductEntity?> GetByIdAsync(string id);
    Task<List<ProductEntity>> GetByCategoryAsync(string category);
    Task<ProductEntity> CreateAsync(ProductEntity product);
    Task<bool> UpdateAsync(ProductEntity product);
    Task<bool> DeleteAsync(string id);
    Task<List<ProductEntity>> SearchAsync(string query);
    Task<List<ProductEntity>> GetByIdsAsync(IEnumerable<string> ids);
    Task<bool> ExistsAsync(string name, string category);
    Task<ProductEntity?> GetByNameAndCategoryAsync(string name, string category);
    Task<(List<ProductEntity> Items, long Total)> GetPagedAsync(int skip, int take);
}

public interface ICategoryRepository
{
    Task<List<Category>> GetAllAsync();
    Task<Category?> GetByIdAsync(string id);
    Task<Category?> GetByNameAsync(string name);
    Task<Category> CreateAsync(Category category);
    Task<bool> UpdateAsync(Category category);
    Task<bool> DeleteAsync(string id);
    Task IncrementProductCountAsync(string categoryName);
}

public interface IEventPublisher
{
    Task PublishAsync<T>(T @event) where T : class;
}
