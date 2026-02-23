using Neo4j.Driver;
using Recommendation.API.Application.DTOs;
using Recommendation.API.Application.Interfaces;
using Recommendation.API.Infrastructure.Data;

namespace Recommendation.API.Infrastructure.Repositories;

public class RecommendationRepository : IRecommendationRepository
{
    private readonly Neo4jContext _context;
    private readonly ILogger<RecommendationRepository> _logger;

    public RecommendationRepository(Neo4jContext context, ILogger<RecommendationRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task EnsureUserExistsAsync(string userId, string name = "", string email = "")
    {
        await _context.ExecuteWriteAsync(@"
            MERGE (u:User {userId: $userId})
            ON CREATE SET u.name = $name, u.email = $email, u.joinedDate = datetime(), u.lastActive = datetime()
            ON MATCH SET u.lastActive = datetime()",
            new { userId, name, email });
    }

    public async Task EnsureProductExistsAsync(string productId, string name, string category, decimal price)
    {
        await _context.ExecuteWriteAsync(@"
            MERGE (p:Product {productId: $productId})
            ON CREATE SET p.name = $name, p.category = $category, p.price = $price,
                          p.viewCount = 0, p.purchaseCount = 0, p.rating = 0.0",
            new { productId, name, category, price = (double)price });
    }

    public async Task RecordPurchaseAsync(string userId, string orderId, List<PurchaseItemDto> items)
    {
        foreach (var item in items)
        {
            await _context.ExecuteWriteAsync(@"
                MATCH (u:User {userId: $userId})
                MERGE (p:Product {productId: $productId})
                ON CREATE SET p.name = '', p.category = '', p.price = 0.0, p.viewCount = 0, p.purchaseCount = 0
                CREATE (u)-[:PURCHASED {orderId: $orderId, purchaseDate: datetime(),
                    quantity: $quantity, price: $price}]->(p)
                WITH p
                SET p.purchaseCount = p.purchaseCount + $quantity",
                new
                {
                    userId,
                    productId = item.ProductId,
                    orderId,
                    quantity = item.Quantity,
                    price = (double)item.Price
                });
        }
    }

    public async Task RecordViewAsync(string userId, string productId)
    {
        await _context.ExecuteWriteAsync(@"
            MERGE (u:User {userId: $userId})
            ON CREATE SET u.name = '', u.email = '', u.joinedDate = datetime(), u.lastActive = datetime()
            MERGE (p:Product {productId: $productId})
            ON CREATE SET p.name = '', p.category = '', p.price = 0.0, p.viewCount = 0, p.purchaseCount = 0
            CREATE (u)-[:VIEWED {viewedAt: datetime()}]->(p)
            WITH p
            SET p.viewCount = p.viewCount + 1",
            new { userId, productId });
    }

    public async Task<List<string>> GetUserPurchaseHistoryIdsAsync(string userId)
    {
        return await _context.ExecuteReadAsync(
            @"MATCH (u:User {userId: $userId})-[:PURCHASED]->(p:Product)
              RETURN p.productId as productId
              ORDER BY p.purchaseCount DESC",
            new { userId },
            record => record["productId"].As<string>());
    }

    public async Task<List<string>> GetSimilarUserIdsAsync(string userId, int limit = 20)
    {
        return await _context.ExecuteReadAsync(
            @"MATCH (u:User {userId: $userId})-[:PURCHASED]->(p:Product)
              <-[:PURCHASED]-(similar:User)
              WHERE similar.userId <> $userId
              WITH similar, COUNT(DISTINCT p) as commonPurchases
              ORDER BY commonPurchases DESC
              LIMIT $limit
              RETURN similar.userId as userId",
            new { userId, limit },
            record => record["userId"].As<string>());
    }

    public async Task<List<string>> GetRecommendedProductIdsAsync(string userId, List<string> similarUserIds, int limit = 50)
    {
        return await _context.ExecuteReadAsync(
            @"MATCH (similar:User)-[:PURCHASED]->(rec:Product)
              WHERE similar.userId IN $similarUserIds
              AND NOT EXISTS {
                MATCH (u:User {userId: $userId})-[:PURCHASED]->(rec)
              }
              WITH rec, COUNT(DISTINCT similar) as popularity
              ORDER BY popularity DESC, rec.purchaseCount DESC
              LIMIT $limit
              RETURN rec.productId as productId",
            new { userId, similarUserIds, limit },
            record => record["productId"].As<string>());
    }

    public async Task<List<string>> GetPersonalizedRecommendationIdsAsync(string userId, int limit = 10)
    {
        var similarUsers = await GetSimilarUserIdsAsync(userId, 20);
        if (!similarUsers.Any())
            return await GetTrendingProductIdsAsync(30, limit);

        return await GetRecommendedProductIdsAsync(userId, similarUsers, limit);
    }

    public async Task<List<string>> GetSimilarProductIdsAsync(string productId, int limit = 5)
    {
        return await _context.ExecuteReadAsync(
            @"MATCH (p:Product {productId: $productId})
              MATCH (similar:Product)
              WHERE similar.productId <> $productId
                AND similar.category = p.category
              RETURN similar.productId as productId
              ORDER BY similar.purchaseCount DESC
              LIMIT $limit",
            new { productId, limit },
            record => record["productId"].As<string>());
    }

    public async Task<List<string>> GetTrendingProductIdsAsync(int days = 7, int limit = 10)
    {
        return await _context.ExecuteReadAsync(
            @"MATCH (u:User)-[r:PURCHASED]->(p:Product)
              WHERE r.purchaseDate >= datetime() - duration({days: $days})
              WITH p, COUNT(r) as recentPurchases
              ORDER BY recentPurchases DESC
              LIMIT $limit
              RETURN p.productId as productId",
            new { days, limit },
            record => record["productId"].As<string>());
    }
}
