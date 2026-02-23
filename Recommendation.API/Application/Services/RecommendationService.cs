using Recommendation.API.Application.Algorithms;
using Recommendation.API.Application.DTOs;
using Recommendation.API.Application.Interfaces;

namespace Recommendation.API.Application.Services;

public class RecommendationService : IRecommendationService
{
    private readonly IRecommendationRepository _repository;
    private readonly IProductServiceClient _productServiceClient;
    private readonly CollaborativeFilteringAlgorithm _collaborativeFilter;
    private readonly ContentBasedFilteringAlgorithm _contentFilter;
    private readonly ScoreCalculator _scoreCalculator;
    private readonly ILogger<RecommendationService> _logger;

    public RecommendationService(
        IRecommendationRepository repository,
        IProductServiceClient productServiceClient,
        CollaborativeFilteringAlgorithm collaborativeFilter,
        ContentBasedFilteringAlgorithm contentFilter,
        ScoreCalculator scoreCalculator,
        ILogger<RecommendationService> logger)
    {
        _repository = repository;
        _productServiceClient = productServiceClient;
        _collaborativeFilter = collaborativeFilter;
        _contentFilter = contentFilter;
        _scoreCalculator = scoreCalculator;
        _logger = logger;
    }

    public async Task<List<RecommendedProductDto>> GetPersonalizedAsync(string userId, int limit = 10)
    {
        var productIds = await _collaborativeFilter.GetRecommendedProductIds(userId, limit);
        var products = await _productServiceClient.GetProductsByIdsAsync(productIds);

        var historyIds = await _repository.GetUserPurchaseHistoryIdsAsync(userId);
        var similarUserIds = await _repository.GetSimilarUserIdsAsync(userId, 5);

        return products.Select(p => new RecommendedProductDto
        {
            ProductId = p.Id,
            Name = p.Name,
            Category = p.Category,
            Price = p.Price,
            ImageUrl = p.ImageUrl,
            Rating = p.Rating,
            Score = _scoreCalculator.CalculateScore(p, historyIds, similarUserIds.Count),
            Reason = _scoreCalculator.GenerateReason(similarUserIds.Count, p.Category),
            Confidence = _scoreCalculator.CalculateConfidence(similarUserIds.Count)
        })
        .OrderByDescending(r => r.Score)
        .ToList();
    }

    public async Task<List<RecommendedProductDto>> GetSimilarProductsAsync(string productId, int limit = 5)
    {
        var productIds = await _contentFilter.GetSimilarProductIds(productId, limit);
        var products = await _productServiceClient.GetProductsByIdsAsync(productIds);

        return products.Select(p => new RecommendedProductDto
        {
            ProductId = p.Id,
            Name = p.Name,
            Category = p.Category,
            Price = p.Price,
            ImageUrl = p.ImageUrl,
            Rating = p.Rating,
            Score = p.Rating / 5.0,
            Reason = "Produit similaire basé sur la catégorie",
            Confidence = 0.7
        }).ToList();
    }

    public async Task<List<RecommendedProductDto>> GetTrendingAsync(int days = 7, int limit = 10)
    {
        var productIds = await _repository.GetTrendingProductIdsAsync(days, limit);
        var products = await _productServiceClient.GetProductsByIdsAsync(productIds);

        return products.Select((p, index) => new RecommendedProductDto
        {
            ProductId = p.Id,
            Name = p.Name,
            Category = p.Category,
            Price = p.Price,
            ImageUrl = p.ImageUrl,
            Rating = p.Rating,
            Score = 1.0 - index * 0.1,
            Reason = $"Tendance des {days} derniers jours",
            Confidence = 0.8
        }).ToList();
    }

    public async Task RecordViewAsync(string userId, string productId)
    {
        await _repository.EnsureUserExistsAsync(userId);
        await _repository.RecordViewAsync(userId, productId);
        _logger.LogDebug("Recorded view: user {UserId} -> product {ProductId}", userId, productId);
    }

    public async Task RecordPurchaseAsync(string userId, string orderId, List<PurchaseItemDto> items)
    {
        await _repository.EnsureUserExistsAsync(userId);

        foreach (var item in items)
        {
            await _repository.EnsureProductExistsAsync(item.ProductId, item.ProductName, "", item.Price);
        }

        await _repository.RecordPurchaseAsync(userId, orderId, items);
        _logger.LogInformation("Recorded purchase: user {UserId}, order {OrderId}", userId, orderId);
    }

    public async Task<List<string>> GetUserHistoryAsync(string userId)
    {
        return await _repository.GetUserPurchaseHistoryIdsAsync(userId);
    }
}

public interface IRecommendationService
{
    Task<List<RecommendedProductDto>> GetPersonalizedAsync(string userId, int limit = 10);
    Task<List<RecommendedProductDto>> GetSimilarProductsAsync(string productId, int limit = 5);
    Task<List<RecommendedProductDto>> GetTrendingAsync(int days = 7, int limit = 10);
    Task RecordViewAsync(string userId, string productId);
    Task RecordPurchaseAsync(string userId, string orderId, List<PurchaseItemDto> items);
    Task<List<string>> GetUserHistoryAsync(string userId);
}
