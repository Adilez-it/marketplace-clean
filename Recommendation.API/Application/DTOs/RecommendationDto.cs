namespace Recommendation.API.Application.DTOs;

public class RecommendedProductDto
{
    public string ProductId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public double Rating { get; set; }
    public double Score { get; set; }
    public string Reason { get; set; } = string.Empty;
    public double Confidence { get; set; }
}

public class RecordViewDto
{
    public string UserId { get; set; } = string.Empty;
    public string ProductId { get; set; } = string.Empty;
}

public class RecordPurchaseDto
{
    public string UserId { get; set; } = string.Empty;
    public string OrderId { get; set; } = string.Empty;
    public List<PurchaseItemDto> Items { get; set; } = new();
}

public class PurchaseItemDto
{
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal Price { get; set; }
}

public class ProductDetailsDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public double Rating { get; set; }
    public int PurchaseCount { get; set; }
    public DateTime CreatedAt { get; set; }
}
