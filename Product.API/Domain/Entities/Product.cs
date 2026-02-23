using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Product.API.Domain.Enums;

namespace Product.API.Domain.Entities;

public class Product
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    [BsonElement("description")]
    public string Description { get; set; } = string.Empty;

    [BsonElement("category")]
    public string Category { get; set; } = string.Empty;

    [BsonElement("price")]
    public decimal Price { get; set; }

    [BsonElement("stock")]
    public int Stock { get; set; }

    [BsonElement("imageUrl")]
    public string ImageUrl { get; set; } = string.Empty;

    [BsonElement("rating")]
    public double Rating { get; set; }

    [BsonElement("reviewCount")]
    public int ReviewCount { get; set; }

    [BsonElement("status")]
    public ProductStatus Status { get; set; } = ProductStatus.Available;

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public void DecrementStock(int quantity)
    {
        if (quantity > Stock)
            throw new InvalidOperationException($"Insufficient stock. Available: {Stock}, Requested: {quantity}");

        Stock -= quantity;
        if (Stock == 0) Status = ProductStatus.OutOfStock;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdatePrice(decimal newPrice)
    {
        if (newPrice <= 0) throw new ArgumentException("Price must be greater than 0");
        Price = newPrice;
        UpdatedAt = DateTime.UtcNow;
    }

    public void AddReview(double rating)
    {
        var totalRating = Rating * ReviewCount + rating;
        ReviewCount++;
        Rating = Math.Round(totalRating / ReviewCount, 2);
        UpdatedAt = DateTime.UtcNow;
    }
}
