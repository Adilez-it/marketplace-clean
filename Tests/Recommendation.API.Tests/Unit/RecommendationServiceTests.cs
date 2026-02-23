using Moq;
using Microsoft.Extensions.Logging;
using Recommendation.API.Application.Algorithms;
using Recommendation.API.Application.DTOs;
using Recommendation.API.Application.Interfaces;
using Recommendation.API.Application.Services;
using Xunit;

namespace Recommendation.API.Tests.Unit;

public class RecommendationServiceTests
{
    private readonly Mock<IRecommendationRepository> _mockRepo;
    private readonly Mock<IProductServiceClient>     _mockProductClient;
    private readonly RecommendationService           _service;

    public RecommendationServiceTests()
    {
        _mockRepo          = new Mock<IRecommendationRepository>();
        _mockProductClient = new Mock<IProductServiceClient>();

        var cfLogger  = new Mock<ILogger<CollaborativeFilteringAlgorithm>>();
        var svcLogger = new Mock<ILogger<RecommendationService>>();

        var collabFilter  = new CollaborativeFilteringAlgorithm(_mockRepo.Object, cfLogger.Object);
        var contentFilter = new ContentBasedFilteringAlgorithm(_mockRepo.Object);
        var scoreCalc     = new ScoreCalculator();

        _service = new RecommendationService(
            _mockRepo.Object, _mockProductClient.Object,
            collabFilter, contentFilter, scoreCalc, svcLogger.Object);
    }

    // ── Helper ───────────────────────────────────────────────────────────────

    private static ProductDetailsDto MakeProduct(string id, string name,
        string category = "Electronics", decimal price = 99m,
        double rating = 4.0, int purchaseCount = 10) => new()
    {
        Id            = id,
        Name          = name,
        Category      = category,
        Price         = price,
        Rating        = rating,
        PurchaseCount = purchaseCount,
        CreatedAt     = DateTime.UtcNow.AddDays(-10)
    };

    // ── GetPersonalizedAsync — cold start ─────────────────────────────────────

    [Fact]
    public async Task GetPersonalizedAsync_ColdStart_ReturnsTrendingProducts()
    {
        _mockRepo.Setup(r => r.GetUserPurchaseHistoryIdsAsync("new-user"))
                 .ReturnsAsync(new List<string>());
        _mockRepo.Setup(r => r.GetTrendingProductIdsAsync(30, 10))
                 .ReturnsAsync(new List<string> { "t1", "t2" });
        _mockProductClient.Setup(c => c.GetProductsByIdsAsync(It.IsAny<IEnumerable<string>>()))
                 .ReturnsAsync(new List<ProductDetailsDto>
                 {
                     MakeProduct("t1", "iPhone 15", purchaseCount: 200),
                     MakeProduct("t2", "MacBook Pro", purchaseCount: 150)
                 });
        _mockRepo.Setup(r => r.GetSimilarUserIdsAsync("new-user", 5))
                 .ReturnsAsync(new List<string>());

        var result = await _service.GetPersonalizedAsync("new-user");

        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        _mockRepo.Verify(r => r.GetTrendingProductIdsAsync(30, 10), Times.Once);
    }

    [Fact]
    public async Task GetPersonalizedAsync_ColdStart_ReturnsEmpty_WhenNoTrending()
    {
        _mockRepo.Setup(r => r.GetUserPurchaseHistoryIdsAsync("u1")).ReturnsAsync(new List<string>());
        _mockRepo.Setup(r => r.GetTrendingProductIdsAsync(30, 10)).ReturnsAsync(new List<string>());
        _mockProductClient.Setup(c => c.GetProductsByIdsAsync(It.IsAny<IEnumerable<string>>()))
                 .ReturnsAsync(new List<ProductDetailsDto>());
        _mockRepo.Setup(r => r.GetSimilarUserIdsAsync("u1", 5)).ReturnsAsync(new List<string>());

        var result = await _service.GetPersonalizedAsync("u1");

        Assert.Empty(result);
    }

    // ── GetPersonalizedAsync — with history ───────────────────────────────────

    [Fact]
    public async Task GetPersonalizedAsync_WithHistory_UsesCollaborativeFiltering()
    {
        _mockRepo.Setup(r => r.GetUserPurchaseHistoryIdsAsync("user123"))
                 .ReturnsAsync(new List<string> { "p-old-1" });
        _mockRepo.Setup(r => r.GetSimilarUserIdsAsync("user123", 20))
                 .ReturnsAsync(new List<string> { "user-a", "user-b", "user-c" });
        _mockRepo.Setup(r => r.GetRecommendedProductIdsAsync("user123",
                 It.IsAny<List<string>>(), It.IsAny<int>()))
                 .ReturnsAsync(new List<string> { "rec1", "rec2" });
        _mockProductClient.Setup(c => c.GetProductsByIdsAsync(It.IsAny<IEnumerable<string>>()))
                 .ReturnsAsync(new List<ProductDetailsDto>
                 {
                     MakeProduct("rec1", "AirPods Pro",  rating: 4.6, purchaseCount: 80),
                     MakeProduct("rec2", "Apple Watch",  rating: 4.4, purchaseCount: 60)
                 });
        _mockRepo.Setup(r => r.GetSimilarUserIdsAsync("user123", 5))
                 .ReturnsAsync(new List<string> { "ua", "ub" });

        var result = await _service.GetPersonalizedAsync("user123");

        Assert.Equal(2, result.Count);
        Assert.True(result[0].Score >= result[1].Score); // trié par score
        _mockRepo.Verify(r => r.GetRecommendedProductIdsAsync(
            "user123", It.IsAny<List<string>>(), It.IsAny<int>()), Times.Once);
    }

    [Fact]
    public async Task GetPersonalizedAsync_WithHistoryButNoSimilarUsers_ReturnsTrending7Days()
    {
        _mockRepo.Setup(r => r.GetUserPurchaseHistoryIdsAsync("u2"))
                 .ReturnsAsync(new List<string> { "p1" });
        _mockRepo.Setup(r => r.GetSimilarUserIdsAsync("u2", 20))
                 .ReturnsAsync(new List<string>());
        _mockRepo.Setup(r => r.GetTrendingProductIdsAsync(7, 10))
                 .ReturnsAsync(new List<string> { "trend1" });
        _mockProductClient.Setup(c => c.GetProductsByIdsAsync(It.IsAny<IEnumerable<string>>()))
                 .ReturnsAsync(new List<ProductDetailsDto> { MakeProduct("trend1", "Samsung TV") });
        _mockRepo.Setup(r => r.GetSimilarUserIdsAsync("u2", 5))
                 .ReturnsAsync(new List<string>());

        var result = await _service.GetPersonalizedAsync("u2");

        Assert.Single(result);
        _mockRepo.Verify(r => r.GetTrendingProductIdsAsync(7, 10), Times.Once);
    }

    [Fact]
    public async Task GetPersonalizedAsync_RespectsLimit()
    {
        _mockRepo.Setup(r => r.GetUserPurchaseHistoryIdsAsync("u")).ReturnsAsync(new List<string>());
        _mockRepo.Setup(r => r.GetTrendingProductIdsAsync(30, 3))
                 .ReturnsAsync(new List<string> { "a", "b", "c" });
        _mockProductClient.Setup(c => c.GetProductsByIdsAsync(It.IsAny<IEnumerable<string>>()))
                 .ReturnsAsync(new List<ProductDetailsDto>
                 {
                     MakeProduct("a","P1"), MakeProduct("b","P2"), MakeProduct("c","P3")
                 });
        _mockRepo.Setup(r => r.GetSimilarUserIdsAsync("u", 5)).ReturnsAsync(new List<string>());

        var result = await _service.GetPersonalizedAsync("u", limit: 3);

        Assert.Equal(3, result.Count);
    }

    // ── GetTrendingAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetTrendingAsync_ReturnsProductsWithTrendingReason()
    {
        _mockRepo.Setup(r => r.GetTrendingProductIdsAsync(7, 10))
                 .ReturnsAsync(new List<string> { "p1", "p2" });
        _mockProductClient.Setup(c => c.GetProductsByIdsAsync(It.IsAny<IEnumerable<string>>()))
                 .ReturnsAsync(new List<ProductDetailsDto>
                 {
                     MakeProduct("p1","Produit A", rating: 4.8),
                     MakeProduct("p2","Produit B", rating: 4.2)
                 });

        var result = await _service.GetTrendingAsync(days: 7, limit: 10);

        Assert.Equal(2, result.Count);
        Assert.All(result, r => Assert.Contains("7", r.Reason));
    }

    [Fact]
    public async Task GetTrendingAsync_ScoreDecreasesWithPosition()
    {
        _mockRepo.Setup(r => r.GetTrendingProductIdsAsync(It.IsAny<int>(), It.IsAny<int>()))
                 .ReturnsAsync(new List<string> { "p1", "p2", "p3" });
        _mockProductClient.Setup(c => c.GetProductsByIdsAsync(It.IsAny<IEnumerable<string>>()))
                 .ReturnsAsync(new List<ProductDetailsDto>
                 {
                     MakeProduct("p1","A"), MakeProduct("p2","B"), MakeProduct("p3","C")
                 });

        var result = await _service.GetTrendingAsync();

        Assert.True(result[0].Score > result[1].Score);
        Assert.True(result[1].Score > result[2].Score);
    }

    [Fact]
    public async Task GetTrendingAsync_ReturnsEmpty_WhenNoProducts()
    {
        _mockRepo.Setup(r => r.GetTrendingProductIdsAsync(It.IsAny<int>(), It.IsAny<int>()))
                 .ReturnsAsync(new List<string>());
        _mockProductClient.Setup(c => c.GetProductsByIdsAsync(It.IsAny<IEnumerable<string>>()))
                 .ReturnsAsync(new List<ProductDetailsDto>());

        var result = await _service.GetTrendingAsync();

        Assert.Empty(result);
    }

    // ── GetSimilarProductsAsync ───────────────────────────────────────────────

    [Fact]
    public async Task GetSimilarProductsAsync_ReturnsSimilarWithFixedConfidence()
    {
        _mockRepo.Setup(r => r.GetSimilarProductIdsAsync("prod-1", 5))
                 .ReturnsAsync(new List<string> { "s1", "s2" });
        _mockProductClient.Setup(c => c.GetProductsByIdsAsync(It.IsAny<IEnumerable<string>>()))
                 .ReturnsAsync(new List<ProductDetailsDto>
                 {
                     MakeProduct("s1","Similar A", rating: 4.0),
                     MakeProduct("s2","Similar B", rating: 3.5)
                 });

        var result = await _service.GetSimilarProductsAsync("prod-1");

        Assert.Equal(2, result.Count);
        Assert.All(result, r =>
        {
            Assert.Equal(0.7, r.Confidence);
            Assert.Contains("similaire", r.Reason);
        });
    }

    [Fact]
    public async Task GetSimilarProductsAsync_ScoreBasedOnRating()
    {
        _mockRepo.Setup(r => r.GetSimilarProductIdsAsync("p", 5))
                 .ReturnsAsync(new List<string> { "s1" });
        _mockProductClient.Setup(c => c.GetProductsByIdsAsync(It.IsAny<IEnumerable<string>>()))
                 .ReturnsAsync(new List<ProductDetailsDto> { MakeProduct("s1","X", rating: 4.5) });

        var result = await _service.GetSimilarProductsAsync("p");

        Assert.Equal(4.5 / 5.0, result[0].Score, precision: 5);
    }

    // ── RecordViewAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task RecordViewAsync_CallsEnsureUserThenRecordView()
    {
        _mockRepo.Setup(r => r.EnsureUserExistsAsync("u", "", "")).Returns(Task.CompletedTask);
        _mockRepo.Setup(r => r.RecordViewAsync("u", "p")).Returns(Task.CompletedTask);

        await _service.RecordViewAsync("u", "p");

        _mockRepo.Verify(r => r.EnsureUserExistsAsync("u", "", ""), Times.Once);
        _mockRepo.Verify(r => r.RecordViewAsync("u", "p"), Times.Once);
    }

    // ── RecordPurchaseAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task RecordPurchaseAsync_EnsuresUserAndProductsExist()
    {
        var items = new List<PurchaseItemDto>
        {
            new() { ProductId = "prod1", ProductName = "iPhone", Quantity = 1, Price = 999m },
            new() { ProductId = "prod2", ProductName = "Case",   Quantity = 2, Price = 19m  }
        };

        _mockRepo.Setup(r => r.EnsureUserExistsAsync("user1", "", "")).Returns(Task.CompletedTask);
        _mockRepo.Setup(r => r.EnsureProductExistsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<decimal>())).Returns(Task.CompletedTask);
        _mockRepo.Setup(r => r.RecordPurchaseAsync("user1", "order1", items)).Returns(Task.CompletedTask);

        await _service.RecordPurchaseAsync("user1", "order1", items);

        _mockRepo.Verify(r => r.EnsureUserExistsAsync("user1", "", ""), Times.Once);
        _mockRepo.Verify(r => r.EnsureProductExistsAsync("prod1", "iPhone", "", 999m), Times.Once);
        _mockRepo.Verify(r => r.EnsureProductExistsAsync("prod2", "Case",   "", 19m),  Times.Once);
        _mockRepo.Verify(r => r.RecordPurchaseAsync("user1", "order1", items), Times.Once);
    }

    // ── GetUserHistoryAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task GetUserHistoryAsync_ReturnsProductIds()
    {
        var expected = new List<string> { "p1", "p2", "p3" };
        _mockRepo.Setup(r => r.GetUserPurchaseHistoryIdsAsync("user1")).ReturnsAsync(expected);

        var result = await _service.GetUserHistoryAsync("user1");

        Assert.Equal(3, result.Count);
        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task GetUserHistoryAsync_ReturnsEmpty_WhenNoHistory()
    {
        _mockRepo.Setup(r => r.GetUserPurchaseHistoryIdsAsync("new")).ReturnsAsync(new List<string>());

        var result = await _service.GetUserHistoryAsync("new");

        Assert.Empty(result);
    }
}

// ═══════════════════════════════════════════════════════
//  ScoreCalculator Tests
// ═══════════════════════════════════════════════════════

public class ScoreCalculatorTests
{
    private readonly ScoreCalculator _calc = new();

    private static ProductDetailsDto Product(int purchaseCount = 50, double rating = 4.0, int daysOld = 10) => new()
    {
        Id = "p1", Name = "Test", Category = "Electronics",
        PurchaseCount = purchaseCount,
        Rating        = rating,
        CreatedAt     = DateTime.UtcNow.AddDays(-daysOld)
    };

    [Fact]
    public void CalculateScore_ReturnsBetween0And1()
    {
        var score = _calc.CalculateScore(Product(), new List<string>(), 5);
        Assert.InRange(score, 0.0, 1.0);
    }

    [Fact]
    public void CalculateScore_HighPurchaseCount_HasHigherScore()
    {
        var high = _calc.CalculateScore(Product(purchaseCount: 200), new List<string>(), 0);
        var low  = _calc.CalculateScore(Product(purchaseCount: 5),   new List<string>(), 0);
        Assert.True(high > low);
    }

    [Fact]
    public void CalculateScore_HighRating_HasHigherScore()
    {
        var high = _calc.CalculateScore(Product(rating: 5.0), new List<string>(), 0);
        var low  = _calc.CalculateScore(Product(rating: 1.0), new List<string>(), 0);
        Assert.True(high > low);
    }

    [Fact]
    public void CalculateScore_RecentProduct_HasHigherScore()
    {
        var recent = _calc.CalculateScore(Product(daysOld: 1),   new List<string>(), 0);
        var old    = _calc.CalculateScore(Product(daysOld: 200), new List<string>(), 0);
        Assert.True(recent > old);
    }

    [Theory]
    [InlineData(0,  0.0)]
    [InlineData(10, 0.5)]
    [InlineData(20, 1.0)]
    [InlineData(50, 1.0)]   // plafonné à 1
    public void CalculateConfidence_IsProportionalToSimilarUsers(int users, double expected)
    {
        Assert.Equal(expected, _calc.CalculateConfidence(users), precision: 5);
    }

    [Theory]
    [InlineData(0,  "catégorie")]
    [InlineData(6,  "similaires")]
    [InlineData(15, "15 utilisateurs")]
    public void GenerateReason_ReturnsCorrectMessage(int users, string expectedPart)
    {
        var reason = _calc.GenerateReason(users, "Electronics");
        Assert.Contains(expectedPart, reason);
    }
}

// ═══════════════════════════════════════════════════════
//  CollaborativeFilteringAlgorithm Tests
// ═══════════════════════════════════════════════════════

public class CollaborativeFilteringAlgorithmTests
{
    private readonly Mock<IRecommendationRepository>               _repo = new();
    private readonly Mock<ILogger<CollaborativeFilteringAlgorithm>> _log  = new();

    private CollaborativeFilteringAlgorithm Algo() =>
        new CollaborativeFilteringAlgorithm(_repo.Object, _log.Object);

    [Fact]
    public async Task GetRecommendedProductIds_ColdStart_CallsTrending30Days()
    {
        _repo.Setup(r => r.GetUserPurchaseHistoryIdsAsync("u")).ReturnsAsync(new List<string>());
        _repo.Setup(r => r.GetTrendingProductIdsAsync(30, 10)).ReturnsAsync(new List<string> { "t1" });

        var result = await Algo().GetRecommendedProductIds("u");

        Assert.Equal(new[] { "t1" }, result);
        _repo.Verify(r => r.GetTrendingProductIdsAsync(30, 10), Times.Once);
    }

    [Fact]
    public async Task GetRecommendedProductIds_NoSimilarUsers_CallsTrending7Days()
    {
        _repo.Setup(r => r.GetUserPurchaseHistoryIdsAsync("u")).ReturnsAsync(new List<string> { "p1" });
        _repo.Setup(r => r.GetSimilarUserIdsAsync("u", 20)).ReturnsAsync(new List<string>());
        _repo.Setup(r => r.GetTrendingProductIdsAsync(7, 10)).ReturnsAsync(new List<string> { "t1" });

        var result = await Algo().GetRecommendedProductIds("u");

        _repo.Verify(r => r.GetTrendingProductIdsAsync(7, 10), Times.Once);
    }

    [Fact]
    public async Task GetRecommendedProductIds_WithSimilarUsers_AppliesLimit()
    {
        _repo.Setup(r => r.GetUserPurchaseHistoryIdsAsync("u")).ReturnsAsync(new List<string> { "p1" });
        _repo.Setup(r => r.GetSimilarUserIdsAsync("u", 20)).ReturnsAsync(new List<string> { "a", "b" });
        // Use It.IsAny — the algorithm creates a NEW list internally, so reference equality fails
        _repo.Setup(r => r.GetRecommendedProductIdsAsync("u", It.IsAny<List<string>>(), It.IsAny<int>()))
             .ReturnsAsync(new List<string> { "r1", "r2", "r3", "r4", "r5" });

        var result = await Algo().GetRecommendedProductIds("u", limit: 2);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetRecommendedProductIds_PassesCorrectSimilarUsersList()
    {
        var similarUsers = new List<string> { "ua", "ub", "uc" };
        _repo.Setup(r => r.GetUserPurchaseHistoryIdsAsync("u")).ReturnsAsync(new List<string> { "p1" });
        _repo.Setup(r => r.GetSimilarUserIdsAsync("u", 20)).ReturnsAsync(similarUsers);
        _repo.Setup(r => r.GetRecommendedProductIdsAsync("u", similarUsers, 50))
             .ReturnsAsync(new List<string> { "r1" });

        await Algo().GetRecommendedProductIds("u");

        _repo.Verify(r => r.GetRecommendedProductIdsAsync("u", similarUsers, 50), Times.Once);
    }
}

// ═══════════════════════════════════════════════════════
//  ContentBasedFilteringAlgorithm Tests
// ═══════════════════════════════════════════════════════

public class ContentBasedFilteringAlgorithmTests
{
    private readonly Mock<IRecommendationRepository> _repo = new();

    [Fact]
    public async Task GetSimilarProductIds_DelegatesToRepository()
    {
        var expected = new List<string> { "s1", "s2", "s3" };
        _repo.Setup(r => r.GetSimilarProductIdsAsync("prod-1", 5)).ReturnsAsync(expected);

        var result = await new ContentBasedFilteringAlgorithm(_repo.Object).GetSimilarProductIds("prod-1");

        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task GetSimilarProductIds_RespectsCustomLimit()
    {
        _repo.Setup(r => r.GetSimilarProductIdsAsync("p1", 3))
             .ReturnsAsync(new List<string> { "a", "b", "c" });

        var result = await new ContentBasedFilteringAlgorithm(_repo.Object).GetSimilarProductIds("p1", limit: 3);

        Assert.Equal(3, result.Count);
        _repo.Verify(r => r.GetSimilarProductIdsAsync("p1", 3), Times.Once);
    }

    [Fact]
    public async Task GetSimilarProductIds_ReturnsEmpty_WhenNoSimilar()
    {
        _repo.Setup(r => r.GetSimilarProductIdsAsync("p1", It.IsAny<int>()))
             .ReturnsAsync(new List<string>());

        var result = await new ContentBasedFilteringAlgorithm(_repo.Object).GetSimilarProductIds("p1");

        Assert.Empty(result);
    }
}
