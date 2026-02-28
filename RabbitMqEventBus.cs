using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Shared.EventBus;

public class RabbitMqEventBus : IEventBus, IDisposable
{
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly IServiceProvider _services;
    private readonly ILogger<RabbitMqEventBus> _logger;
    private readonly Dictionary<string, List<Type>> _handlers = new();

    public RabbitMqEventBus(
        IConnection connection, IServiceProvider services,
        ILogger<RabbitMqEventBus> logger)
    {
        _connection = connection;
        _channel = _connection.CreateModel();
        _services = services;
        _logger = logger;
        _channel.ExchangeDeclare("event_bus", ExchangeType.Topic, durable: true);
    }

    public async Task PublishAsync<T>(T @event, CancellationToken ct) where T : IntegrationEvent
    {
        var eventName = typeof(T).Name;
        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(@event));
        var props = _channel.CreateBasicProperties();
        props.Persistent = true;
        props.MessageId = @event.Id.ToString();
        _channel.BasicPublish("event_bus", eventName, props, body);
        _logger.LogInformation("Published {Event} ({Id})", eventName, @event.Id);
    }

    public void Subscribe<T, TH>() where T : IntegrationEvent where TH : IEventHandler<T>
    {
        var eventName = typeof(T).Name;
        var queueName = $"{eventName}_{typeof(TH).Name}";
        _channel.QueueDeclare(queueName, durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object>
            {
                { "x-dead-letter-exchange", "event_bus_dlx" },
                { "x-message-ttl", 30000 }
            });
        _channel.QueueBind(queueName, "event_bus", eventName);
        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.Received += async (_, ea) =>
        {
            var body = Encoding.UTF8.GetString(ea.Body.Span);
            var @event = JsonSerializer.Deserialize<T>(body)!;
            using var scope = _services.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<TH>();
            await handler.HandleAsync(@event);
            _channel.BasicAck(ea.DeliveryTag, false);
        };
        _channel.BasicConsume(queueName, autoAck: false, consumer);
    }

    public void Dispose() => _channel?.Dispose();
}
