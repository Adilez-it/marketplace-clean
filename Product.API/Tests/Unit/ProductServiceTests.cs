using Moq;
using Microsoft.Extensions.Logging;
using Product.API.Application.DTOs;
using Product.API.Application.Interfaces;
using Product.API.Application.Services;
using Product.API.Domain.Events;
using Xunit;

namespace Product.API.Tests.Unit;

public class ProductServiceTests
{
    private readonly Mock<IProductRepository> _mockRepo;
    private readonly Mock<ICategoryRepository> _mockCategoryRepo;
    private readonly Mock<IEventPublisher> _mockPublisher;
    private readonly Mock<ILogger<ProductService>> _mockLogger;
    private readonly ProductService _service;

    public ProductServiceTests()
    {
        _mockRepo = new Mock<IProductRepository>();
        _mockCategoryRepo = new Mock<ICategoryRepository>();
        _mockPublisher = new Mock<IEventPublisher>();
        _mockLogger = new Mock<ILogger<ProductService>>();
        _service = new ProductService(_mockRepo.Object, _mockCategoryRepo.Object, _mockPublisher.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task CreateProductAsync_ShouldReturnProduct_WhenValidDto()
    {
        // Arrange
        var dto = new CreateProductDto
        {
            Name = "Test Product",
            Description = "Test Description",
            Category = "Electronics",
            Price = 99.99m,
            Stock = 10,
            ImageUrl = "https://example.com/image.jpg"
        };

        _mockRepo.Setup(r => r.CreateAsync(It.IsAny<Domain.Entities.Product>()))
                 .ReturnsAsync((Domain.Entities.Product p) => p);
        _mockCategoryRepo.Setup(r => r.IncrementProductCountAsync(It.IsAny<string>()))
                         .Returns(Task.CompletedTask);
        _mockPublisher.Setup(p => p.PublishAsync(It.IsAny<ProductCreatedEvent>()))
                      .Returns(Task.CompletedTask);

        // Act
        var result = await _service.CreateProductAsync(dto);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Test Product", result.Name);
        Assert.Equal(99.99m, result.Price);
        Assert.Equal(10, result.Stock);
        _mockPublisher.Verify(p => p.PublishAsync(It.IsAny<ProductCreatedEvent>()), Times.Once);
    }

    [Fact]
    public async Task GetProductByIdAsync_ShouldReturnNull_WhenProductNotFound()
    {
        // Arrange
        _mockRepo.Setup(r => r.GetByIdAsync(It.IsAny<string>()))
                 .ReturnsAsync((Domain.Entities.Product?)null);

        // Act
        var result = await _service.GetProductByIdAsync("nonexistent");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetProductByIdAsync_ShouldReturnProduct_WhenFound()
    {
        // Arrange
        var product = new Domain.Entities.Product
        {
            Id = "test-id",
            Name = "iPhone 15",
            Price = 999m,
            Stock = 50
        };

        _mockRepo.Setup(r => r.GetByIdAsync("test-id")).ReturnsAsync(product);

        // Act
        var result = await _service.GetProductByIdAsync("test-id");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("test-id", result.Id);
        Assert.Equal("iPhone 15", result.Name);
    }

    [Fact]
    public async Task DecrementStockAsync_ShouldReturnFalse_WhenProductNotFound()
    {
        // Arrange
        _mockRepo.Setup(r => r.GetByIdAsync(It.IsAny<string>()))
                 .ReturnsAsync((Domain.Entities.Product?)null);

        // Act
        var result = await _service.DecrementStockAsync("nonexistent", 1);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task DecrementStockAsync_ShouldThrow_WhenInsufficientStock()
    {
        // Arrange
        var product = new Domain.Entities.Product { Id = "p1", Stock = 2 };
        _mockRepo.Setup(r => r.GetByIdAsync("p1")).ReturnsAsync(product);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.DecrementStockAsync("p1", 5));
    }

    [Fact]
    public async Task UpdateProductAsync_ShouldReturnFalse_WhenProductNotFound()
    {
        // Arrange
        _mockRepo.Setup(r => r.GetByIdAsync(It.IsAny<string>()))
                 .ReturnsAsync((Domain.Entities.Product?)null);

        // Act
        var result = await _service.UpdateProductAsync("nonexistent", new UpdateProductDto());

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task DeleteProductAsync_ShouldCallRepository()
    {
        // Arrange
        _mockRepo.Setup(r => r.DeleteAsync("p1")).ReturnsAsync(true);

        // Act
        var result = await _service.DeleteProductAsync("p1");

        // Assert
        Assert.True(result);
        _mockRepo.Verify(r => r.DeleteAsync("p1"), Times.Once);
    }

    [Fact]
    public async Task GetProductsAsync_ShouldReturnAllProducts()
    {
        // Arrange
        var products = new List<Domain.Entities.Product>
        {
            new() { Id = "1", Name = "Product 1", Price = 10m },
            new() { Id = "2", Name = "Product 2", Price = 20m },
            new() { Id = "3", Name = "Product 3", Price = 30m }
        };

        _mockRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(products);

        // Act
        var result = await _service.GetProductsAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Count);
    }
}
