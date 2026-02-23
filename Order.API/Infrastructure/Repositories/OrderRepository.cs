using MongoDB.Driver;
using Order.API.Application.Interfaces;
using Order.API.Domain.Entities;
using Order.API.Domain.Enums;
using Order.API.Infrastructure.Data;

namespace Order.API.Infrastructure.Repositories;

public class OrderRepository : IOrderRepository
{
    private readonly MongoDbContext _context;

    public OrderRepository(MongoDbContext context)
    {
        _context = context;
    }

    public async Task<List<CustomerOrder>> GetAllAsync() =>
        await _context.Orders.Find(_ => true).SortByDescending(o => o.CreatedAt).ToListAsync();

    public async Task<CustomerOrder?> GetByIdAsync(string id) =>
        await _context.Orders.Find(o => o.Id == id).FirstOrDefaultAsync();

    public async Task<List<CustomerOrder>> GetByUserIdAsync(string userId) =>
        await _context.Orders.Find(o => o.UserId == userId).SortByDescending(o => o.CreatedAt).ToListAsync();

    public async Task<CustomerOrder> CreateAsync(CustomerOrder order)
    {
        await _context.Orders.InsertOneAsync(order);
        return order;
    }

    public async Task<bool> UpdateAsync(CustomerOrder order)
    {
        var result = await _context.Orders.ReplaceOneAsync(o => o.Id == order.Id, order);
        return result.ModifiedCount > 0;
    }

    public async Task<bool> UpdateStatusAsync(string id, OrderStatus status)
    {
        var update = Builders<CustomerOrder>.Update
            .Set(o => o.Status, status)
            .Set(o => o.UpdatedAt, DateTime.UtcNow);
        var result = await _context.Orders.UpdateOneAsync(o => o.Id == id, update);
        return result.ModifiedCount > 0;
    }

    public async Task<bool> DeleteAsync(string id)
    {
        var result = await _context.Orders.DeleteOneAsync(o => o.Id == id);
        return result.DeletedCount > 0;
    }
}
