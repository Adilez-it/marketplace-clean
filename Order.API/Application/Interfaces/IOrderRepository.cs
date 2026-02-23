using Order.API.Domain.Entities;
using Order.API.Domain.Enums;

namespace Order.API.Application.Interfaces;

public interface IOrderRepository
{
    Task<List<CustomerOrder>> GetAllAsync();
    Task<CustomerOrder?> GetByIdAsync(string id);
    Task<List<CustomerOrder>> GetByUserIdAsync(string userId);
    Task<CustomerOrder> CreateAsync(CustomerOrder order);
    Task<bool> UpdateAsync(CustomerOrder order);
    Task<bool> UpdateStatusAsync(string id, OrderStatus status);
    Task<bool> DeleteAsync(string id);
}

public interface IEventPublisher
{
    Task PublishAsync<T>(T @event) where T : class;
}

public interface IProductServiceClient
{
    Task<bool> CheckStockAsync(string productId, int quantity);
    Task<bool> DecrementStockAsync(string productId, int quantity);
}
