using System.Text;
using System.Text.Json;
using Order.API.Application.Interfaces;
using RabbitMQ.Client;

namespace Order.API.Infrastructure.Messaging;

public class RabbitMqSettings
{
    public string HostName { get; set; } = "localhost";
    public string UserName { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public int Port { get; set; } = 5672;
    public string ExchangeName { get; set; } = "marketplace.events";
}

public class EventPublisher : IEventPublisher, IDisposable
{
    private IConnection? _connection;
    private IModel? _channel;
    private readonly string _exchangeName;

    public EventPublisher(RabbitMqSettings settings)
    {
        _exchangeName = settings.ExchangeName;
        for (var i = 1; i <= 6; i++)
        {
            try
            {
                var factory = new ConnectionFactory
                {
                    HostName = settings.HostName,
                    UserName = settings.UserName,
                    Password = settings.Password,
                    Port = settings.Port,
                    RequestedConnectionTimeout = TimeSpan.FromSeconds(10)
                };
                _connection = factory.CreateConnection();
                _channel = _connection.CreateModel();
                _channel.ExchangeDeclare(_exchangeName, ExchangeType.Topic, durable: true);
                Console.WriteLine($"[Order.API] RabbitMQ connected to {settings.HostName}");
                return;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Order.API] RabbitMQ attempt {i}/6 failed: {ex.Message}. Retrying in 5s...");
                if (i == 6) Console.WriteLine("[Order.API] RabbitMQ unavailable - events will be dropped.");
                else Thread.Sleep(5000);
            }
        }
    }

    public async Task PublishAsync<T>(T @event) where T : class
    {
        if (_channel == null || !_channel.IsOpen) return;
        try
        {
            var eventName = typeof(T).Name;

            // Routing keys must match what Recommendation.API consumer listens on
            var routingKey = eventName switch
            {
                "OrderCreatedEvent"       => "order.created",
                "OrderStatusChangedEvent" => "order.status.changed",
                "OrderCancelledEvent"     => "order.cancelled",
                _                         => $"order.{eventName.ToLower()}"
            };

            var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(@event));
            var props = _channel.CreateBasicProperties();
            props.Persistent = true;
            props.ContentType = "application/json";
            props.Type = eventName;
            _channel.BasicPublish(_exchangeName, routingKey, props, body);
            Console.WriteLine($"[Order.API] Published {eventName} -> {routingKey}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Order.API] Publish error: {ex.Message}");
        }
        await Task.CompletedTask;
    }

    public void Dispose()
    {
        try { _channel?.Close(); } catch { }
        try { _connection?.Close(); } catch { }
    }
}
