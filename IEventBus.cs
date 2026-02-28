namespace Shared.EventBus;

public interface IEventBus
{
    Task PublishAsync<T>(T @event, CancellationToken ct = default) where T : IntegrationEvent;
    void Subscribe<T, TH>() where T : IntegrationEvent where TH : IEventHandler<T>;
}

public abstract record IntegrationEvent
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}

public interface IEventHandler<in T> where T : IntegrationEvent
{
    Task HandleAsync(T @event, CancellationToken ct = default);
}
