namespace Recommendation.API.Domain.Entities;

public class UserNode
{
    public string UserId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime JoinedDate { get; set; } = DateTime.UtcNow;
    public DateTime LastActive { get; set; } = DateTime.UtcNow;
    public List<string> PreferredCategories { get; set; } = new();
}

public class ProductNode
{
    public string ProductId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int ViewCount { get; set; }
    public int PurchaseCount { get; set; }
    public double Rating { get; set; }
}

public class PurchaseRelation
{
    public string OrderId { get; set; } = string.Empty;
    public DateTime PurchaseDate { get; set; } = DateTime.UtcNow;
    public int Quantity { get; set; }
    public decimal Price { get; set; }
}

public class ViewRelation
{
    public DateTime ViewedAt { get; set; } = DateTime.UtcNow;
    public int Duration { get; set; }
    public string Source { get; set; } = string.Empty;
}
