using MongoDB.Bson;
using MongoDB.Driver;
using Product.API.Application.Interfaces;
using Product.API.Infrastructure.Data;
using Product.API.Domain.Entities;
using ProductEntity = Product.API.Domain.Entities.Product;

namespace Product.API.Infrastructure.Repositories;

public class ProductRepository : IProductRepository
{
    private readonly MongoDbContext _context;

    public ProductRepository(MongoDbContext context)
    {
        _context = context;
    }

    public async Task<List<ProductEntity>> GetAllAsync() =>
        await _context.Products.Find(_ => true).ToListAsync();

    public async Task<ProductEntity?> GetByIdAsync(string id) =>
        await _context.Products.Find(p => p.Id == id).FirstOrDefaultAsync();

    public async Task<List<ProductEntity>> GetByCategoryAsync(string category) =>
        await _context.Products
            .Find(p => p.Category.ToLower() == category.ToLower())
            .ToListAsync();

    public async Task<ProductEntity> CreateAsync(ProductEntity product)
    {
        await _context.Products.InsertOneAsync(product);
        return product;
    }

    public async Task<bool> UpdateAsync(ProductEntity product)
    {
        var result = await _context.Products.ReplaceOneAsync(p => p.Id == product.Id, product);
        return result.ModifiedCount > 0;
    }

    public async Task<bool> DeleteAsync(string id)
    {
        var result = await _context.Products.DeleteOneAsync(p => p.Id == id);
        return result.DeletedCount > 0;
    }

    public async Task<List<ProductEntity>> SearchAsync(string query)
    {
        var filter = Builders<ProductEntity>.Filter.Or(
            Builders<ProductEntity>.Filter.Regex(p => p.Name, new BsonRegularExpression(query, "i")),
            Builders<ProductEntity>.Filter.Regex(p => p.Description, new BsonRegularExpression(query, "i")),
            Builders<ProductEntity>.Filter.Regex(p => p.Category, new BsonRegularExpression(query, "i"))
        );

        return await _context.Products.Find(filter).ToListAsync();
    }

    public async Task<List<ProductEntity>> GetByIdsAsync(IEnumerable<string> ids)
    {
        var filter = Builders<ProductEntity>.Filter.In(p => p.Id, ids);
        return await _context.Products.Find(filter).ToListAsync();
    }

    public async Task<bool> ExistsAsync(string name, string category)
    {
        return await _context.Products
            .Find(p =>
                p.Name.ToLower() == name.ToLower() &&
                p.Category.ToLower() == category.ToLower())
            .AnyAsync();
    }

    public async Task<ProductEntity?> GetByNameAndCategoryAsync(string name, string category)
    {
        return await _context.Products.Find(p =>
            p.Name.ToLower() == name.ToLower() &&
            p.Category.ToLower() == category.ToLower())
            .FirstOrDefaultAsync();
    }

    public async Task<(List<ProductEntity> Items, long Total)> GetPagedAsync(int skip, int take)
    {
        var total = await _context.Products.CountDocumentsAsync(_ => true);

        var items = await _context.Products
            .Find(_ => true)
            .Skip(skip)
            .Limit(take)
            .SortByDescending(p => p.CreatedAt)
            .ToListAsync();

        return (items, total);
    }
}