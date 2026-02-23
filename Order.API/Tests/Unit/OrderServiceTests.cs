using Moq;
using Microsoft.Extensions.Logging;
using Order.API.Application.DTOs;
using Order.API.Application.Interfaces;
using Order.API.Application.Services;
using Order.API.Domain.Entities;
using Order.API.Domain.Enums;
using Order.API.Domain.Events;
using Xunit;

namespace Order.API.Tests.Unit;

public class OrderServiceTests
{
    private readonly Mock<IOrderRepository> _mockRepo;
    private readonly Mock<IProductServiceClient> _mockProductClient;
    private readonly Mock<IEventPublisher> _mockPublisher;
    private readonly Mock<ILogger<OrderService>> _mockLogger;
    private readonly OrderService _service;

    public OrderServiceTests()
    {
        _mockRepo          = new Mock<IOrderRepository>();
        _mockProductClient = new Mock<IProductServiceClient>();
        _mockPublisher     = new Mock<IEventPublisher>();
        _mockLogger        = new Mock<ILogger<OrderService>>();
        _service           = new OrderService(
            _mockRepo.Object, _mockProductClient.Object,
            _mockPublisher.Object, _mockLogger.Object);
    }

    // ─── CreateOrderAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task CreateOrderAsync_ShouldSucceed_WhenStockAvailable()
    {
        // Arrange
        var dto = new CreateOrderDto
        {
            UserId   = "user123",
            UserName = "John Doe",
            Items    = new List<CreateOrderItemDto>
            {
                new() { ProductId = "prod1", ProductName = "iPhone 15", Quantity = 1, UnitPrice = 999m }
            },
            ShippingAddress = new AddressDto { Street = "123 Main St", City = "Paris", Country = "France" },
            PaymentInfo     = new PaymentDto  { CardName = "John Doe", CardNumber = "4111111111111111", PaymentMethod = PaymentMethod.CreditCard }
        };

        _mockProductClient.Setup(c => c.CheckStockAsync("prod1", 1)).ReturnsAsync(true);
        _mockProductClient.Setup(c => c.DecrementStockAsync("prod1", 1)).ReturnsAsync(true);
        _mockRepo.Setup(r => r.CreateAsync(It.IsAny<CustomerOrder>()))
                 .ReturnsAsync((CustomerOrder o) => o);
        _mockRepo.Setup(r => r.UpdateStatusAsync(It.IsAny<string>(), It.IsAny<OrderStatus>())).ReturnsAsync(true);
        _mockRepo.Setup(r => r.GetByIdAsync(It.IsAny<string>())).ReturnsAsync((CustomerOrder?)null);
        _mockPublisher.Setup(p => p.PublishAsync(It.IsAny<OrderCreatedEvent>())).Returns(Task.CompletedTask);
        _mockPublisher.Setup(p => p.PublishAsync(It.IsAny<OrderStatusChangedEvent>())).Returns(Task.CompletedTask);

        // Act
        var result = await _service.CreateOrderAsync(dto);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("user123", result.UserId);
        Assert.Equal(999m, result.TotalAmount);
        _mockPublisher.Verify(p => p.PublishAsync(It.IsAny<OrderCreatedEvent>()), Times.Once);
    }

    [Fact]
    public async Task CreateOrderAsync_ShouldThrow_WhenStockUnavailable()
    {
        // Arrange
        var dto = new CreateOrderDto
        {
            UserId = "user123",
            Items  = new List<CreateOrderItemDto>
            {
                new() { ProductId = "prod1", Quantity = 100, UnitPrice = 99m }
            },
            ShippingAddress = new AddressDto(),
            PaymentInfo     = new PaymentDto()
        };

        _mockProductClient.Setup(c => c.CheckStockAsync("prod1", 100)).ReturnsAsync(false);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.CreateOrderAsync(dto));
    }

    [Fact]
    public async Task CreateOrderAsync_ShouldMaskCardNumber()
    {
        // Arrange
        var dto = new CreateOrderDto
        {
            UserId   = "user1",
            UserName = "Alice",
            Items    = new List<CreateOrderItemDto>
            {
                new() { ProductId = "p1", ProductName = "Watch", Quantity = 1, UnitPrice = 299m }
            },
            ShippingAddress = new AddressDto { Country = "FR" },
            PaymentInfo     = new PaymentDto { CardNumber = "4111111111111111", PaymentMethod = PaymentMethod.CreditCard }
        };

        CustomerOrder? capturedOrder = null;
        _mockProductClient.Setup(c => c.CheckStockAsync("p1", 1)).ReturnsAsync(true);
        _mockProductClient.Setup(c => c.DecrementStockAsync("p1", 1)).ReturnsAsync(true);
        _mockRepo.Setup(r => r.CreateAsync(It.IsAny<CustomerOrder>()))
                 .Callback<CustomerOrder>(o => capturedOrder = o)
                 .ReturnsAsync((CustomerOrder o) => o);
        _mockRepo.Setup(r => r.UpdateStatusAsync(It.IsAny<string>(), It.IsAny<OrderStatus>())).ReturnsAsync(true);
        _mockRepo.Setup(r => r.GetByIdAsync(It.IsAny<string>())).ReturnsAsync((CustomerOrder?)null);
        _mockPublisher.Setup(p => p.PublishAsync(It.IsAny<object>())).Returns(Task.CompletedTask);

        // Act
        await _service.CreateOrderAsync(dto);

        // Assert — carte masquée
        Assert.NotNull(capturedOrder);
        Assert.StartsWith("****", capturedOrder!.PaymentInfo.CardNumber);
        Assert.EndsWith("1111", capturedOrder.PaymentInfo.CardNumber);
    }

    [Fact]
    public async Task CreateOrderAsync_MultipleItems_CalculatesTotalCorrectly()
    {
        // Arrange
        var dto = new CreateOrderDto
        {
            UserId   = "user1",
            UserName = "Bob",
            Items    = new List<CreateOrderItemDto>
            {
                new() { ProductId = "p1", ProductName = "Laptop", Quantity = 1,  UnitPrice = 1000m },
                new() { ProductId = "p2", ProductName = "Mouse",  Quantity = 2,  UnitPrice = 25m   },
                new() { ProductId = "p3", ProductName = "Bag",    Quantity = 1,  UnitPrice = 50m   }
            },
            ShippingAddress = new AddressDto(),
            PaymentInfo     = new PaymentDto { PaymentMethod = PaymentMethod.CreditCard }
        };

        _mockProductClient.Setup(c => c.CheckStockAsync(It.IsAny<string>(), It.IsAny<int>())).ReturnsAsync(true);
        _mockProductClient.Setup(c => c.DecrementStockAsync(It.IsAny<string>(), It.IsAny<int>())).ReturnsAsync(true);
        _mockRepo.Setup(r => r.CreateAsync(It.IsAny<CustomerOrder>())).ReturnsAsync((CustomerOrder o) => o);
        _mockRepo.Setup(r => r.UpdateStatusAsync(It.IsAny<string>(), It.IsAny<OrderStatus>())).ReturnsAsync(true);
        _mockRepo.Setup(r => r.GetByIdAsync(It.IsAny<string>())).ReturnsAsync((CustomerOrder?)null);
        _mockPublisher.Setup(p => p.PublishAsync(It.IsAny<object>())).Returns(Task.CompletedTask);

        // Act
        var result = await _service.CreateOrderAsync(dto);

        // Assert: 1000 + (2×25) + 50 = 1100
        Assert.Equal(1100m, result.TotalAmount);
    }

    // ─── CancelOrderAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task CancelOrderAsync_ShouldSucceed_WhenOrderIsPending()
    {
        var order = new CustomerOrder { Id = "order1", Status = OrderStatus.Pending };
        _mockRepo.Setup(r => r.GetByIdAsync("order1")).ReturnsAsync(order);
        _mockRepo.Setup(r => r.UpdateStatusAsync("order1", OrderStatus.Cancelled)).ReturnsAsync(true);
        _mockPublisher.Setup(p => p.PublishAsync(It.IsAny<OrderCancelledEvent>())).Returns(Task.CompletedTask);

        var result = await _service.CancelOrderAsync("order1", "User cancelled");

        Assert.True(result);
        _mockRepo.Verify(r => r.UpdateStatusAsync("order1", OrderStatus.Cancelled), Times.Once);
    }

    [Fact]
    public async Task CancelOrderAsync_ShouldSucceed_WhenOrderIsConfirmed()
    {
        var order = new CustomerOrder { Id = "order2", Status = OrderStatus.Confirmed };
        _mockRepo.Setup(r => r.GetByIdAsync("order2")).ReturnsAsync(order);
        _mockRepo.Setup(r => r.UpdateStatusAsync("order2", OrderStatus.Cancelled)).ReturnsAsync(true);
        _mockPublisher.Setup(p => p.PublishAsync(It.IsAny<OrderCancelledEvent>())).Returns(Task.CompletedTask);

        var result = await _service.CancelOrderAsync("order2", "Changed mind");

        Assert.True(result);
    }

    [Fact]
    public async Task CancelOrderAsync_ShouldThrow_WhenOrderIsDelivered()
    {
        var order = new CustomerOrder { Id = "order1", Status = OrderStatus.Delivered };
        _mockRepo.Setup(r => r.GetByIdAsync("order1")).ReturnsAsync(order);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CancelOrderAsync("order1", "Too late"));
    }

    [Fact]
    public async Task CancelOrderAsync_ShouldThrow_WhenOrderIsShipped()
    {
        var order = new CustomerOrder { Id = "order1", Status = OrderStatus.Shipped };
        _mockRepo.Setup(r => r.GetByIdAsync("order1")).ReturnsAsync(order);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CancelOrderAsync("order1", "Already shipped"));
    }

    [Fact]
    public async Task CancelOrderAsync_ShouldReturnFalse_WhenOrderNotFound()
    {
        _mockRepo.Setup(r => r.GetByIdAsync("nonexistent")).ReturnsAsync((CustomerOrder?)null);

        var result = await _service.CancelOrderAsync("nonexistent", "reason");

        Assert.False(result);
    }

    // ─── UpdateOrderStatusAsync ──────────────────────────────────────────────

    [Fact]
    public async Task UpdateOrderStatusAsync_ShouldReturnFalse_WhenOrderNotFound()
    {
        _mockRepo.Setup(r => r.GetByIdAsync("x")).ReturnsAsync((CustomerOrder?)null);

        var result = await _service.UpdateOrderStatusAsync("x", OrderStatus.Shipped);

        Assert.False(result);
    }

    [Fact]
    public async Task UpdateOrderStatusAsync_ShouldPublishEvent_WhenSuccessful()
    {
        var order = new CustomerOrder { Id = "o1", Status = OrderStatus.Confirmed };
        _mockRepo.Setup(r => r.GetByIdAsync("o1")).ReturnsAsync(order);
        _mockRepo.Setup(r => r.UpdateStatusAsync("o1", OrderStatus.Shipped)).ReturnsAsync(true);
        _mockPublisher.Setup(p => p.PublishAsync(It.IsAny<OrderStatusChangedEvent>())).Returns(Task.CompletedTask);

        var result = await _service.UpdateOrderStatusAsync("o1", OrderStatus.Shipped);

        Assert.True(result);
        _mockPublisher.Verify(p => p.PublishAsync(It.IsAny<OrderStatusChangedEvent>()), Times.Once);
    }

    // ─── GetOrdersByUserIdAsync ──────────────────────────────────────────────

    [Fact]
    public async Task GetOrdersByUserIdAsync_ShouldReturnUserOrders()
    {
        var orders = new List<CustomerOrder>
        {
            new() { Id = "o1", UserId = "user123" },
            new() { Id = "o2", UserId = "user123" }
        };
        _mockRepo.Setup(r => r.GetByUserIdAsync("user123")).ReturnsAsync(orders);

        var result = await _service.GetOrdersByUserIdAsync("user123");

        Assert.Equal(2, result.Count);
        Assert.All(result, o => Assert.Equal("user123", o.UserId));
    }

    [Fact]
    public async Task GetOrdersByUserIdAsync_ShouldReturnEmpty_WhenNoOrders()
    {
        _mockRepo.Setup(r => r.GetByUserIdAsync("newuser")).ReturnsAsync(new List<CustomerOrder>());

        var result = await _service.GetOrdersByUserIdAsync("newuser");

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetOrderByIdAsync_ShouldReturnNull_WhenNotFound()
    {
        _mockRepo.Setup(r => r.GetByIdAsync("xxx")).ReturnsAsync((CustomerOrder?)null);

        var result = await _service.GetOrderByIdAsync("xxx");

        Assert.Null(result);
    }
}
