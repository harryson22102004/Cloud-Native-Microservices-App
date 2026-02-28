using OrderService.DTOs;
using OrderService.Models;
using OrderService.Repositories;
using Shared.EventBus;

namespace OrderService.Services;

public record OrderCreatedEvent(Guid OrderId, decimal Total, string CustomerEmail) : IntegrationEvent;

public class OrderServiceImpl : IOrderService
{
    private readonly IOrderRepository _repo;
    private readonly IEventBus _eventBus;
    private readonly ILogger<OrderServiceImpl> _logger;

    public OrderServiceImpl(IOrderRepository repo, IEventBus eventBus, ILogger<OrderServiceImpl> logger)
    {
        _repo = repo;
        _eventBus = eventBus;
        _logger = logger;
    }

    public async Task<Order> CreateAsync(CreateOrderDto dto, CancellationToken ct)
    {
        var order = new Order
        {
            Id = Guid.NewGuid(),
            CustomerId = dto.CustomerId,
            Items = dto.Items.Select(i => new OrderItem
            {
                ProductId = i.ProductId,
                Quantity = i.Quantity,
                UnitPrice = i.Price,
            }).ToList(),
            Status = OrderStatus.Pending,
            CreatedAt = DateTime.UtcNow,
        };
        order.TotalAmount = order.Items.Sum(i => i.Quantity * i.UnitPrice);

        await _repo.AddAsync(order, ct);
        await _eventBus.PublishAsync(
            new OrderCreatedEvent(order.Id, order.TotalAmount, dto.CustomerEmail), ct);
        _logger.LogInformation("Order {OrderId} created, total={Total}", order.Id, order.TotalAmount);
        return order;
    }

    public Task<Order?> GetByIdAsync(Guid id, CancellationToken ct) => _repo.GetByIdAsync(id, ct);

    public async Task UpdateStatusAsync(Guid id, OrderStatus status, CancellationToken ct)
    {
        var order = await _repo.GetByIdAsync(id, ct) ?? throw new KeyNotFoundException();
        order.Status = status;
        order.UpdatedAt = DateTime.UtcNow;
        await _repo.UpdateAsync(order, ct);
    }
}
