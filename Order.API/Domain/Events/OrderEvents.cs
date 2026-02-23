using Order.API.Domain.Enums;

namespace Order.API.Domain.Events;

public class OrderCreatedEvent
{
    public string OrderId { get; set; } = string.Empty;
    public string OrderNumber { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public List<OrderEventItem> Items { get; set; } = new();
    public decimal TotalAmount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class OrderStatusChangedEvent
{
    public string OrderId { get; set; } = string.Empty;
    public OrderStatus OldStatus { get; set; }
    public OrderStatus NewStatus { get; set; }
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
}

public class OrderCancelledEvent
{
    public string OrderId { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public DateTime CancelledAt { get; set; } = DateTime.UtcNow;
}

// Renamed from OrderItemDto to avoid conflict with Application.DTOs.OrderItemDto
public class OrderEventItem
{
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}
