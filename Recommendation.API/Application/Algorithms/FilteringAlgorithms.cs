using Recommendation.API.Application.DTOs;
using Recommendation.API.Application.Interfaces;

namespace Recommendation.API.Application.Algorithms;

public class CollaborativeFilteringAlgorithm
{
    private readonly IRecommendationRepository _repository;
    private readonly ILogger<CollaborativeFilteringAlgorithm> _logger;

    public CollaborativeFilteringAlgorithm(IRecommendationRepository repository, ILogger<CollaborativeFilteringAlgorithm> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<List<string>> GetRecommendedProductIds(string userId, int limit = 10)
    {
        var userHistory = await _repository.GetUserPurchaseHistoryIdsAsync(userId);

        if (!userHistory.Any())
        {
            _logger.LogInformation("No purchase history for user {UserId}, returning trending", userId);
            return await _repository.GetTrendingProductIdsAsync(30, limit);
        }

        var similarUsers = await _repository.GetSimilarUserIdsAsync(userId, 20);

        if (!similarUsers.Any())
        {
            return await _repository.GetTrendingProductIdsAsync(7, limit);
        }

        var candidateIds = await _repository.GetRecommendedProductIdsAsync(userId, similarUsers, limit * 5);
        return candidateIds.Take(limit).ToList();
    }
}

public class ContentBasedFilteringAlgorithm
{
    private readonly IRecommendationRepository _repository;

    public ContentBasedFilteringAlgorithm(IRecommendationRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<string>> GetSimilarProductIds(string productId, int limit = 5)
    {
        return await _repository.GetSimilarProductIdsAsync(productId, limit);
    }
}

public class ScoreCalculator
{
    public double CalculateScore(ProductDetailsDto product, List<string> userHistoryIds, int similarUsersCount)
    {
        const double popularityWeight = 0.4;
        const double recencyWeight = 0.3;
        const double ratingWeight = 0.3;

        var popularityScore = Math.Min(product.PurchaseCount / 100.0, 1.0) * popularityWeight;

        var daysSinceCreation = (DateTime.UtcNow - product.CreatedAt).Days;
        var recencyScore = Math.Max(0, 1.0 - daysSinceCreation / 90.0) * recencyWeight;

        var ratingScore = product.Rating / 5.0 * ratingWeight;

        return popularityScore + recencyScore + ratingScore;
    }

    public string GenerateReason(int similarUsersCount, string category)
    {
        return similarUsersCount switch
        {
            > 10 => $"{similarUsersCount} utilisateurs similaires ont acheté ce produit",
            > 5 => "Populaire parmi les utilisateurs ayant des goûts similaires",
            _ => $"Basé sur votre intérêt pour la catégorie {category}"
        };
    }

    public double CalculateConfidence(int similarUsersCount)
    {
        return Math.Min(similarUsersCount / 20.0, 1.0);
    }
}
