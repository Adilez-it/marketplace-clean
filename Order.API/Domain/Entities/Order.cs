using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Order.API.Domain.Enums;

namespace Order.API.Domain.Entities;

public class CustomerOrder
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    [BsonElement("orderNumber")]
    public string OrderNumber { get; set; } = GenerateOrderNumber();

    [BsonElement("userId")]
    public string UserId { get; set; } = string.Empty;

    [BsonElement("userName")]
    public string UserName { get; set; } = string.Empty;

    [BsonElement("totalAmount")]
    public decimal TotalAmount { get; set; }

    [BsonElement("status")]
    public OrderStatus Status { get; set; } = OrderStatus.Pending;

    [BsonElement("orderItems")]
    public List<OrderItem> OrderItems { get; set; } = new();

    [BsonElement("shippingAddress")]
    public Address ShippingAddress { get; set; } = new();

    [BsonElement("paymentInfo")]
    public Payment PaymentInfo { get; set; } = new();

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public decimal CalculateTotal() => OrderItems.Sum(i => i.TotalPrice);

    public void UpdateStatus(OrderStatus newStatus)
    {
        Status = newStatus;
        UpdatedAt = DateTime.UtcNow;
    }

    public bool CanBeCancelled() =>
        Status == OrderStatus.Pending || Status == OrderStatus.Confirmed;

    private static string GenerateOrderNumber() =>
        $"ORD-{DateTime.UtcNow:yyyy}-{Random.Shared.Next(1000, 9999):D4}";
}

public class OrderItem
{
    [BsonElement("productId")]
    public string ProductId { get; set; } = string.Empty;

    [BsonElement("productName")]
    public string ProductName { get; set; } = string.Empty;

    [BsonElement("quantity")]
    public int Quantity { get; set; }

    [BsonElement("unitPrice")]
    public decimal UnitPrice { get; set; }

    [BsonElement("totalPrice")]
    public decimal TotalPrice => Quantity * UnitPrice;
}

public class Address
{
    [BsonElement("street")]
    public string Street { get; set; } = string.Empty;

    [BsonElement("city")]
    public string City { get; set; } = string.Empty;

    [BsonElement("state")]
    public string State { get; set; } = string.Empty;

    [BsonElement("country")]
    public string Country { get; set; } = string.Empty;

    [BsonElement("zipCode")]
    public string ZipCode { get; set; } = string.Empty;

    [BsonElement("phoneNumber")]
    public string PhoneNumber { get; set; } = string.Empty;
}

public class Payment
{
    [BsonElement("cardName")]
    public string CardName { get; set; } = string.Empty;

    [BsonElement("cardNumber")]
    public string CardNumber { get; set; } = string.Empty;

    [BsonElement("expiration")]
    public string Expiration { get; set; } = string.Empty;

    [BsonElement("cvv")]
    public string CVV { get; set; } = string.Empty;

    [BsonElement("paymentMethod")]
    public PaymentMethod PaymentMethod { get; set; }
}
