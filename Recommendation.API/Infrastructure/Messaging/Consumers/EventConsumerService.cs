using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Recommendation.API.Application.DTOs;
using Recommendation.API.Application.Services;

namespace Recommendation.API.Infrastructure.Messaging.Consumers;

public class OrderCreatedEventMessage
{
    public string OrderId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public List<OrderItemMessage> Items { get; set; } = new();
    public decimal TotalAmount { get; set; }
}

public class OrderItemMessage
{
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}

public class ProductViewedEventMessage
{
    public string ProductId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
}

public class RabbitMqConsumerSettings
{
    public string HostName { get; set; } = "localhost";
    public string UserName { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public int Port { get; set; } = 5672;
    public string ExchangeName { get; set; } = "marketplace.events";
}

public class EventConsumerService : BackgroundService
{
    private readonly RabbitMqConsumerSettings _settings;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<EventConsumerService> _logger;
    private IConnection? _connection;
    private IModel? _channel;

    public EventConsumerService(
        RabbitMqConsumerSettings settings,
        IServiceProvider serviceProvider,
        ILogger<EventConsumerService> logger)
    {
        _settings = settings;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(8000, stoppingToken);
        try
        {
            var factory = new ConnectionFactory
            {
                HostName = _settings.HostName,
                UserName = _settings.UserName,
                Password = _settings.Password,
                Port = _settings.Port,
                DispatchConsumersAsync = true
            };

            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();
            _channel.ExchangeDeclare(_settings.ExchangeName, ExchangeType.Topic, durable: true);

            var orderQueue = _channel.QueueDeclare("recommendation.order.created", durable: true, exclusive: false, autoDelete: false);
            _channel.QueueBind(orderQueue.QueueName, _settings.ExchangeName, "order.created");

            var viewQueue = _channel.QueueDeclare("recommendation.product.viewed", durable: true, exclusive: false, autoDelete: false);
            _channel.QueueBind(viewQueue.QueueName, _settings.ExchangeName, "product.viewed");

            var orderConsumer = new AsyncEventingBasicConsumer(_channel);
            orderConsumer.Received += HandleOrderCreated;
            _channel.BasicConsume(orderQueue.QueueName, false, orderConsumer);

            var viewConsumer = new AsyncEventingBasicConsumer(_channel);
            viewConsumer.Received += HandleProductViewed;
            _channel.BasicConsume(viewQueue.QueueName, false, viewConsumer);

            _logger.LogInformation("Recommendation consumers started");
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { _logger.LogError(ex, "Consumer startup error"); }
    }

    private async Task HandleOrderCreated(object sender, BasicDeliverEventArgs ea)
    {
        try
        {
            var msg = JsonSerializer.Deserialize<OrderCreatedEventMessage>(
                Encoding.UTF8.GetString(ea.Body.ToArray()),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (msg != null)
            {
                using var scope = _serviceProvider.CreateScope();
                var svc = scope.ServiceProvider.GetRequiredService<IRecommendationService>();
                await svc.RecordPurchaseAsync(msg.UserId, msg.OrderId,
                    msg.Items.Select(i => new PurchaseItemDto
                    {
                        ProductId = i.ProductId, ProductName = i.ProductName,
                        Quantity = i.Quantity, Price = i.UnitPrice
                    }).ToList());
            }
            _channel?.BasicAck(ea.DeliveryTag, false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling OrderCreatedEvent");
            _channel?.BasicNack(ea.DeliveryTag, false, requeue: true);
        }
    }

    private async Task HandleProductViewed(object sender, BasicDeliverEventArgs ea)
    {
        try
        {
            var msg = JsonSerializer.Deserialize<ProductViewedEventMessage>(
                Encoding.UTF8.GetString(ea.Body.ToArray()),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (msg != null)
            {
                using var scope = _serviceProvider.CreateScope();
                var svc = scope.ServiceProvider.GetRequiredService<IRecommendationService>();
                await svc.RecordViewAsync(msg.UserId, msg.ProductId);
            }
            _channel?.BasicAck(ea.DeliveryTag, false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling ProductViewedEvent");
            _channel?.BasicNack(ea.DeliveryTag, false, requeue: false);
        }
    }

    public override void Dispose() { _channel?.Close(); _connection?.Close(); base.Dispose(); }
}
