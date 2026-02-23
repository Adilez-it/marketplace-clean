using MongoDB.Driver;
using Product.API.Application.Interfaces;
using Product.API.Domain.Entities;
using Product.API.Infrastructure.Data;

namespace Product.API.Infrastructure.Repositories;

public class CategoryRepository : ICategoryRepository
{
    private readonly MongoDbContext _context;

    public CategoryRepository(MongoDbContext context)
    {
        _context = context;
    }

    public async Task<List<Category>> GetAllAsync() =>
        await _context.Categories.Find(_ => true).ToListAsync();

    public async Task<Category?> GetByIdAsync(string id) =>
        await _context.Categories.Find(c => c.Id == id).FirstOrDefaultAsync();

    public async Task<Category?> GetByNameAsync(string name) =>
        await _context.Categories.Find(c => c.Name.ToLower() == name.ToLower()).FirstOrDefaultAsync();

    public async Task<Category> CreateAsync(Category category)
    {
        await _context.Categories.InsertOneAsync(category);
        return category;
    }

    public async Task<bool> UpdateAsync(Category category)
    {
        var result = await _context.Categories.ReplaceOneAsync(c => c.Id == category.Id, category);
        return result.ModifiedCount > 0;
    }

    public async Task<bool> DeleteAsync(string id)
    {
        var result = await _context.Categories.DeleteOneAsync(c => c.Id == id);
        return result.DeletedCount > 0;
    }

    public async Task IncrementProductCountAsync(string categoryName)
    {
        var update = Builders<Category>.Update.Inc(c => c.ProductCount, 1);
        await _context.Categories.UpdateOneAsync(
            c => c.Name.ToLower() == categoryName.ToLower(),
            update);
    }
}
