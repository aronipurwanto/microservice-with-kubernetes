using SharedModels.Models;
using System.Collections.Concurrent;

namespace OrderService.Services;

public interface IOrderService
{
    Task<OrderResponseDto> CreateOrderAsync(OrderCreateDto orderCreateDto);
    Task<OrderResponseDto> GetOrderAsync(Guid id);
    Task<List<OrderSummaryDto>> GetUserOrdersAsync(Guid userId);
    Task<OrderResponseDto> UpdateOrderStatusAsync(Guid id, UpdateOrderStatusDto updateDto);
    Task<bool> DeleteOrderAsync(Guid id);
}

public class OrderService : IOrderService
{
    private static readonly ConcurrentDictionary<Guid, Order> _orders = new();
    private static readonly ConcurrentDictionary<Guid, OrderItem> _orderItems = new();

    public Task<OrderResponseDto> CreateOrderAsync(OrderCreateDto orderCreateDto)
    {
        var order = new Order
        {
            Id = Guid.NewGuid(),
            UserId = orderCreateDto.UserId,
            OrderDate = DateTime.UtcNow,
            Status = OrderStatus.Pending,
            ShippingAddress = orderCreateDto.ShippingAddress,
            CustomerNotes = orderCreateDto.CustomerNotes,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        decimal totalAmount = 0;
        var orderItems = new List<OrderItem>();

        foreach (var itemDto in orderCreateDto.OrderItems)
        {
            var orderItem = new OrderItem
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                ProductId = itemDto.ProductId,
                ProductName = itemDto.ProductName,
                ProductDescription = itemDto.ProductDescription,
                Quantity = itemDto.Quantity,
                UnitPrice = itemDto.UnitPrice
            };

            totalAmount += orderItem.TotalPrice;
            orderItems.Add(orderItem);
            _orderItems[orderItem.Id] = orderItem;
        }

        order.TotalAmount = totalAmount;
        order.OrderItems = orderItems;
        
        _orders[order.Id] = order;

        return Task.FromResult(MapToOrderResponseDto(order));
    }

    public Task<OrderResponseDto> GetOrderAsync(Guid id)
    {
        if (_orders.TryGetValue(id, out var order))
        {
            return Task.FromResult(MapToOrderResponseDto(order));
        }
        return Task.FromResult<OrderResponseDto>(null);
    }

    public Task<List<OrderSummaryDto>> GetUserOrdersAsync(Guid userId)
    {
        var userOrders = _orders.Values
            .Where(o => o.UserId == userId)
            .Select(order => new OrderSummaryDto
            {
                Id = order.Id,
                UserId = order.UserId,
                OrderDate = order.OrderDate,
                TotalAmount = order.TotalAmount,
                Status = order.Status,
                TotalItems = order.OrderItems.Sum(oi => oi.Quantity),
                CreatedAt = order.CreatedAt
            })
            .ToList();

        return Task.FromResult(userOrders);
    }

    public Task<OrderResponseDto> UpdateOrderStatusAsync(Guid id, UpdateOrderStatusDto updateDto)
    {
        if (_orders.TryGetValue(id, out var order))
        {
            order.Status = updateDto.Status;
            order.UpdatedAt = DateTime.UtcNow;
            _orders[id] = order;

            return Task.FromResult(MapToOrderResponseDto(order));
        }
        return Task.FromResult<OrderResponseDto>(null);
    }

    public Task<bool> DeleteOrderAsync(Guid id)
    {
        return Task.FromResult(_orders.TryRemove(id, out _));
    }

    private static OrderResponseDto MapToOrderResponseDto(Order order)
    {
        return new OrderResponseDto
        {
            Id = order.Id,
            UserId = order.UserId,
            OrderDate = order.OrderDate,
            TotalAmount = order.TotalAmount,
            Status = order.Status,
            ShippingAddress = order.ShippingAddress,
            CustomerNotes = order.CustomerNotes,
            CreatedAt = order.CreatedAt,
            UpdatedAt = order.UpdatedAt,
            OrderItems = order.OrderItems.Select(oi => new OrderItemResponseDto
            {
                Id = oi.Id,
                ProductId = oi.ProductId,
                ProductName = oi.ProductName,
                ProductDescription = oi.ProductDescription,
                Quantity = oi.Quantity,
                UnitPrice = oi.UnitPrice
            }).ToList()
        };
    }
}