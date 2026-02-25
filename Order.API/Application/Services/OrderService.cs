using Order.API.Application.DTOs;
using Order.API.Application.Interfaces;
using Order.API.Domain.Entities;
using Order.API.Domain.Enums;
using Order.API.Domain.Events;

namespace Order.API.Application.Services;

public class OrderService : IOrderService
{
    private readonly IOrderRepository _repository;
    private readonly IProductServiceClient _productServiceClient;
    private readonly IEventPublisher _eventPublisher;
    private readonly ILogger<OrderService> _logger;

    public OrderService(
        IOrderRepository repository,
        IProductServiceClient productServiceClient,
        IEventPublisher eventPublisher,
        ILogger<OrderService> logger)
    {
        _repository = repository;
        _productServiceClient = productServiceClient;
        _eventPublisher = eventPublisher;
        _logger = logger;
    }

    public async Task<List<OrderDto>> GetOrdersAsync()
    {
        var orders = await _repository.GetAllAsync();
        return orders.Select(MapToDto).ToList();
    }

    public async Task<OrderDto?> GetOrderByIdAsync(string id)
    {
        var order = await _repository.GetByIdAsync(id);
        return order == null ? null : MapToDto(order);
    }

    public async Task<List<OrderDto>> GetOrdersByUserIdAsync(string userId)
    {
        var orders = await _repository.GetByUserIdAsync(userId);
        return orders.Select(MapToDto).ToList();
    }

    // This method checks stock for each item, creates the order, decrements stock, updates status, and publishes events.
    public async Task<OrderDto> CreateOrderAsync(CreateOrderDto dto)
    {
        foreach (var item in dto.Items)
        {
            var stockAvailable = await _productServiceClient.CheckStockAsync(item.ProductId, item.Quantity);
            if (!stockAvailable)
                throw new InvalidOperationException($"Insufficient stock for product {item.ProductId}");
        }

        var order = new CustomerOrder
        {
            UserId = dto.UserId,
            UserName = dto.UserName,
            OrderItems = dto.Items.Select(i => new OrderItem
            {
                ProductId = i.ProductId,
                ProductName = i.ProductName,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice
            }).ToList(),
            ShippingAddress = new Address
            {
                Street = dto.ShippingAddress.Street,
                City = dto.ShippingAddress.City,
                State = dto.ShippingAddress.State,
                Country = dto.ShippingAddress.Country,
                ZipCode = dto.ShippingAddress.ZipCode,
                PhoneNumber = dto.ShippingAddress.PhoneNumber
            },
            PaymentInfo = new Payment
            {
                CardName = dto.PaymentInfo.CardName,
                CardNumber = MaskCardNumber(dto.PaymentInfo.CardNumber),
                Expiration = dto.PaymentInfo.Expiration,
                PaymentMethod = dto.PaymentInfo.PaymentMethod
            }
        };

        order.TotalAmount = order.CalculateTotal();
        var created = await _repository.CreateAsync(order);

        foreach (var item in dto.Items)
            await _productServiceClient.DecrementStockAsync(item.ProductId, item.Quantity);

        await UpdateOrderStatusAsync(created.Id, OrderStatus.Confirmed);

        await _eventPublisher.PublishAsync(new OrderCreatedEvent
        {
            OrderId = created.Id,
            OrderNumber = created.OrderNumber,
            UserId = created.UserId,
            Items = created.OrderItems.Select(i => new Domain.Events.OrderEventItem
            {
                ProductId = i.ProductId,
                ProductName = i.ProductName,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice
            }).ToList(),
            TotalAmount = created.TotalAmount
        });

        _logger.LogInformation("Order created: {OrderId} for user {UserId}", created.Id, created.UserId);
        return MapToDto(created);
    }

    public async Task<bool> UpdateOrderStatusAsync(string id, OrderStatus newStatus)
    {
        var order = await _repository.GetByIdAsync(id);
        if (order == null) return false;

        var oldStatus = order.Status;
        var result = await _repository.UpdateStatusAsync(id, newStatus);

        if (result)
        {
            await _eventPublisher.PublishAsync(new OrderStatusChangedEvent
            {
                OrderId = id,
                OldStatus = oldStatus,
                NewStatus = newStatus
            });
        }

        return result;
    }

    public async Task<bool> CancelOrderAsync(string id, string reason = "User cancelled")
    {
        var order = await _repository.GetByIdAsync(id);
        if (order == null) return false;
        if (!order.CanBeCancelled())
            throw new InvalidOperationException($"Order {id} cannot be cancelled in status {order.Status}");

        var result = await _repository.UpdateStatusAsync(id, OrderStatus.Cancelled);

        if (result)
        {
            await _eventPublisher.PublishAsync(new OrderCancelledEvent
            {
                OrderId = id,
                Reason = reason
            });
        }

        return result;
    }

    // This method masks the card number except for the last 4 digits for security reasons.
    private static string MaskCardNumber(string cardNumber)
    {
        if (cardNumber.Length < 4) return cardNumber;
        return "****" + cardNumber[^4..];
    }

    // This method maps a CustomerOrder domain entity to an OrderDto for API responses.
    private static OrderDto MapToDto(CustomerOrder order) => new()
    {
        Id = order.Id,
        OrderNumber = order.OrderNumber,
        UserId = order.UserId,
        UserName = order.UserName,
        TotalAmount = order.TotalAmount,
        Status = order.Status,
        OrderItems = order.OrderItems.Select(i => new OrderItemDto
        {
            ProductId = i.ProductId,
            ProductName = i.ProductName,
            Quantity = i.Quantity,
            UnitPrice = i.UnitPrice,
            TotalPrice = i.TotalPrice
        }).ToList(),
        ShippingAddress = new AddressDto
        {
            Street = order.ShippingAddress.Street,
            City = order.ShippingAddress.City,
            State = order.ShippingAddress.State,
            Country = order.ShippingAddress.Country,
            ZipCode = order.ShippingAddress.ZipCode,
            PhoneNumber = order.ShippingAddress.PhoneNumber
        },
        CreatedAt = order.CreatedAt,
        UpdatedAt = order.UpdatedAt
    };
}

// This interface defines the contract for the OrderService, including methods for retrieving, creating, updating, and cancelling orders.
public interface IOrderService
{
    Task<List<OrderDto>> GetOrdersAsync();
    Task<OrderDto?> GetOrderByIdAsync(string id);
    Task<List<OrderDto>> GetOrdersByUserIdAsync(string userId);
    Task<OrderDto> CreateOrderAsync(CreateOrderDto dto);
    Task<bool> UpdateOrderStatusAsync(string id, OrderStatus newStatus);
    Task<bool> CancelOrderAsync(string id, string reason);
}
