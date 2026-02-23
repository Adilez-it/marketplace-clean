using Recommendation.API.Application.DTOs;

namespace Recommendation.API.Application.Interfaces;

public interface IRecommendationRepository
{
    Task<List<string>> GetPersonalizedRecommendationIdsAsync(string userId, int limit = 10);
    Task<List<string>> GetSimilarProductIdsAsync(string productId, int limit = 5);
    Task<List<string>> GetTrendingProductIdsAsync(int days = 7, int limit = 10);
    Task<List<string>> GetUserPurchaseHistoryIdsAsync(string userId);
    Task<List<string>> GetSimilarUserIdsAsync(string userId, int limit = 20);
    Task<List<string>> GetRecommendedProductIdsAsync(string userId, List<string> similarUserIds, int limit = 50);
    Task RecordPurchaseAsync(string userId, string orderId, List<PurchaseItemDto> items);
    Task RecordViewAsync(string userId, string productId);
    Task EnsureUserExistsAsync(string userId, string name = "", string email = "");
    Task EnsureProductExistsAsync(string productId, string name, string category, decimal price);
}

public interface IProductServiceClient
{
    Task<List<ProductDetailsDto>> GetProductsByIdsAsync(IEnumerable<string> ids);
    Task<ProductDetailsDto?> GetProductByIdAsync(string id);
}
