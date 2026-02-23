using Product.API.Application.DTOs;
using Product.API.Application.Interfaces;
using Product.API.Domain.Entities;
using Product.API.Domain.Events;
using Microsoft.Extensions.Logging;
using ProductEntity = Product.API.Domain.Entities.Product;

namespace Product.API.Application.Services;

public class ProductService : IProductService
{
    private readonly IProductRepository _repository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IEventPublisher _eventPublisher;
    private readonly ILogger<ProductService> _logger;

    public ProductService(
        IProductRepository repository,
        ICategoryRepository categoryRepository,
        IEventPublisher eventPublisher,
        ILogger<ProductService> logger)
    {
        _repository = repository;
        _categoryRepository = categoryRepository;
        _eventPublisher = eventPublisher;
        _logger = logger;
    }

    public async Task<List<ProductDto>> GetProductsAsync()
    {
        var products = await _repository.GetAllAsync();
        return products.Select(MapToDto).ToList();
    }

    public async Task<ProductDto?> GetProductByIdAsync(string id)
    {
        var product = await _repository.GetByIdAsync(id);
        return product == null ? null : MapToDto(product);
    }

    public async Task<List<ProductDto>> GetProductsByCategoryAsync(string category)
    {
        var products = await _repository.GetByCategoryAsync(category.Trim());
        return products.Select(MapToDto).ToList();
    }

    public async Task<List<ProductDto>> SearchProductsAsync(string query)
    {
        var products = await _repository.SearchAsync(query.Trim());
        return products.Select(MapToDto).ToList();
    }

    public async Task<List<ProductDto>> GetProductsByIdsAsync(IEnumerable<string> ids)
    {
        var products = await _repository.GetByIdsAsync(ids);
        return products.Select(MapToDto).ToList();
    }

    public async Task<ProductDto> CreateProductAsync(CreateProductDto dto)
    {
        var product = new ProductEntity
        {
            Id = MongoDB.Bson.ObjectId.GenerateNewId().ToString(),
            Name = dto.Name.Trim(),
            Description = dto.Description,
            Category = dto.Category.Trim(),
            Price = dto.Price,
            Stock = dto.Stock,
            ImageUrl = dto.ImageUrl,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _repository.CreateAsync(product);
        await _categoryRepository.IncrementProductCountAsync(dto.Category);

        await _eventPublisher.PublishAsync(new ProductCreatedEvent
        {
            ProductId = product.Id,
            Name = product.Name,
            Category = product.Category,
            Price = product.Price
        });

        _logger.LogInformation("Product created {ProductId}", product.Id);

        return MapToDto(product);
    }

    public async Task<ProductDto> CreateOrUpdateProductAsync(CreateProductDto dto)
    {
        dto.Name = dto.Name.Trim();
        dto.Category = dto.Category.Trim();

        var existing = await _repository.GetByNameAndCategoryAsync(dto.Name, dto.Category);

        if (existing != null)
        {
            var oldStock = existing.Stock;

            existing.Stock += dto.Stock;
            existing.UpdatedAt = DateTime.UtcNow;

            await _repository.UpdateAsync(existing);

            await _eventPublisher.PublishAsync(new StockUpdatedEvent
            {
                ProductId = existing.Id,
                OldStock = oldStock,
                NewStock = existing.Stock
            });

            _logger.LogInformation("Stock updated for product {ProductId}", existing.Id);

            return MapToDto(existing);
        }

        var product = new ProductEntity
        {
            Id = MongoDB.Bson.ObjectId.GenerateNewId().ToString(),
            Name = dto.Name,
            Description = dto.Description,
            Category = dto.Category,
            Price = dto.Price,
            Stock = dto.Stock,
            ImageUrl = dto.ImageUrl,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _repository.CreateAsync(product);
        await _categoryRepository.IncrementProductCountAsync(dto.Category);

        await _eventPublisher.PublishAsync(new ProductCreatedEvent
        {
            ProductId = product.Id,
            Name = product.Name,
            Category = product.Category,
            Price = product.Price
        });

        _logger.LogInformation("Product created {ProductId}", product.Id);

        return MapToDto(product);
    }

    public async Task<bool> UpdateProductAsync(string id, UpdateProductDto dto)
    {
        var product = await _repository.GetByIdAsync(id);
        if (product == null) return false;

        if (dto.Name != null) product.Name = dto.Name.Trim();
        if (dto.Description != null) product.Description = dto.Description;
        if (dto.Category != null) product.Category = dto.Category.Trim();
        if (dto.Price.HasValue) product.Price = dto.Price.Value;
        if (dto.Stock.HasValue)
        {
            if (dto.Stock.Value < 0)
                throw new ArgumentException("Stock cannot be negative.");

            product.Stock = dto.Stock.Value;
        }

        if (dto.ImageUrl != null) product.ImageUrl = dto.ImageUrl;

        product.UpdatedAt = DateTime.UtcNow;

        var result = await _repository.UpdateAsync(product);

        if (result)
        {
            await _eventPublisher.PublishAsync(new ProductUpdatedEvent
            {
                ProductId = product.Id,
                Name = product.Name,
                Price = product.Price
            });

            _logger.LogInformation("Product updated {ProductId}", product.Id);
        }

        return result;
    }

    public async Task<bool> DeleteProductAsync(string id)
    {
        var result = await _repository.DeleteAsync(id);

        if (result)
            _logger.LogInformation("Product deleted {ProductId}", id);

        return result;
    }

    public async Task<bool> DecrementStockAsync(string id, int quantity)
    {
        if (quantity <= 0)
            throw new ArgumentException("Quantity must be greater than zero.");

        var product = await _repository.GetByIdAsync(id);
        if (product == null) return false;

        if (product.Stock < quantity)
            throw new InvalidOperationException("Insufficient stock.");

        var oldStock = product.Stock;

        product.DecrementStock(quantity);
        product.UpdatedAt = DateTime.UtcNow;

        var result = await _repository.UpdateAsync(product);

        if (result)
        {
            await _eventPublisher.PublishAsync(new StockUpdatedEvent
            {
                ProductId = id,
                OldStock = oldStock,
                NewStock = product.Stock
            });

            _logger.LogInformation("Stock decremented for {ProductId}", id);
        }

        return result;
    }

    public async Task RecordViewAsync(string productId, string userId)
    {
        await _eventPublisher.PublishAsync(new ProductViewedEvent
        {
            ProductId = productId,
            UserId = userId
        });
    }

    public async Task<PagedResult<ProductDto>> GetPagedAsync(int page, int pageSize)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 100 ? 10 : pageSize;

        var skip = (page - 1) * pageSize;

        var (items, total) = await _repository.GetPagedAsync(skip, pageSize);

        return new PagedResult<ProductDto>
        {
            Items = items.Select(MapToDto).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalItems = total
        };
    }

    private static ProductDto MapToDto(ProductEntity product) => new()
    {
        Id = product.Id,
        Name = product.Name,
        Description = product.Description,
        Category = product.Category,
        Price = product.Price,
        Stock = product.Stock,
        ImageUrl = product.ImageUrl,
        Rating = product.Rating,
        ReviewCount = product.ReviewCount,
        Status = product.Status,
        CreatedAt = product.CreatedAt,
        UpdatedAt = product.UpdatedAt
    };
}
