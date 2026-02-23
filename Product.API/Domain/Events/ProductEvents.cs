namespace Product.API.Domain.Events;

public class ProductCreatedEvent
{
    public string ProductId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class ProductViewedEvent
{
    public string ProductId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public DateTime ViewedAt { get; set; } = DateTime.UtcNow;
}

public class StockUpdatedEvent
{
    public string ProductId { get; set; } = string.Empty;
    public int OldStock { get; set; }
    public int NewStock { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class ProductUpdatedEvent
{
    public string ProductId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
