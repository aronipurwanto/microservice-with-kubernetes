using Microsoft.EntityFrameworkCore;
using OrderService.Models;
using SharedModels.Models;

namespace OrderService.Services;

public interface IOrderService
{
    Task<OrderResponseDto?> GetOrderAsync(Guid id);
    Task<OrderResponseDto> CreateOrderAsync(OrderCreateDto orderDto);
    Task<bool> UpdateOrderStatusAsync(Guid id, UpdateOrderStatusDto statusDto);
    Task<List<OrderResponseDto>> GetOrdersByUserAsync(Guid userId);
    Task<List<OrderSummaryDto>> GetOrdersAsync(int page = 1, int pageSize = 20);
    Task<List<OrderResponseDto>> GetOrdersByStatusAsync(OrderStatus status);
    Task<bool> CancelOrderAsync(Guid id, string? reason = null);
}

public class OrderService : IOrderService
{
    private readonly OrderContext _context;
    private readonly ILogger<OrderService> _logger;

    public OrderService(OrderContext context, ILogger<OrderService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<OrderResponseDto?> GetOrderAsync(Guid id)
    {
        var order = await _context.Orders
            .Include(o => o.OrderItems)
            .FirstOrDefaultAsync(o => o.Id == id);

        return order == null ? null : MapToDto(order);
    }

    public async Task<OrderResponseDto> CreateOrderAsync(OrderCreateDto orderDto)
    {
        var order = new Order
        {
            Id = Guid.NewGuid(),
            UserId = orderDto.UserId,
            OrderDate = DateTime.UtcNow,
            Status = OrderStatus.Pending,
            ShippingAddress = orderDto.ShippingAddress,
            CustomerNotes = orderDto.CustomerNotes,
            CreatedAt = DateTime.UtcNow
        };

        // Calculate total amount and create order items
        decimal totalAmount = 0;
        foreach (var itemDto in orderDto.OrderItems)
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
            order.OrderItems.Add(orderItem);
        }
        order.TotalAmount = totalAmount;

        _context.Orders.Add(order);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Created order with ID: {OrderId} for user ID: {UserId}", 
            order.Id, order.UserId);
        
        return MapToDto(order);
    }

    public async Task<bool> UpdateOrderStatusAsync(Guid id, UpdateOrderStatusDto statusDto)
    {
        var order = await _context.Orders.FindAsync(id);
        if (order == null) return false;

        order.Status = statusDto.Status;
        order.UpdatedAt = DateTime.UtcNow;
        
        if (!string.IsNullOrEmpty(statusDto.Notes))
        {
            order.CustomerNotes = string.IsNullOrEmpty(order.CustomerNotes) 
                ? statusDto.Notes 
                : $"{order.CustomerNotes}\n[Status Update]: {statusDto.Notes}";
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Updated order {OrderId} status to {Status}", id, statusDto.Status);
        return true;
    }

    public async Task<List<OrderResponseDto>> GetOrdersByUserAsync(Guid userId)
    {
        var orders = await _context.Orders
            .Include(o => o.OrderItems)
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        return orders.Select(MapToDto).ToList();
    }

    public async Task<List<OrderSummaryDto>> GetOrdersAsync(int page = 1, int pageSize = 20)
    {
        var orders = await _context.Orders
            .Include(o => o.OrderItems)
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return orders.Select(MapToSummaryDto).ToList();
    }

    public async Task<List<OrderResponseDto>> GetOrdersByStatusAsync(OrderStatus status)
    {
        var orders = await _context.Orders
            .Include(o => o.OrderItems)
            .Where(o => o.Status == status)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        return orders.Select(MapToDto).ToList();
    }

    public async Task<bool> CancelOrderAsync(Guid id, string? reason = null)
    {
        var order = await _context.Orders.FindAsync(id);
        if (order == null) return false;

        // Only allow cancellation for orders that are not already completed, shipped, or cancelled
        if (order.Status >= OrderStatus.Shipped)
        {
            throw new InvalidOperationException($"Cannot cancel order with status: {order.Status}");
        }

        order.Status = OrderStatus.Cancelled;
        order.UpdatedAt = DateTime.UtcNow;
        
        if (!string.IsNullOrEmpty(reason))
        {
            order.CustomerNotes = string.IsNullOrEmpty(order.CustomerNotes) 
                ? $"Cancelled: {reason}" 
                : $"{order.CustomerNotes}\n[Cancelled]: {reason}";
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Cancelled order {OrderId}. Reason: {Reason}", id, reason ?? "No reason provided");
        return true;
    }

    private static OrderResponseDto MapToDto(Order order)
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

    private static OrderSummaryDto MapToSummaryDto(Order order)
    {
        return new OrderSummaryDto
        {
            Id = order.Id,
            UserId = order.UserId,
            OrderDate = order.OrderDate,
            TotalAmount = order.TotalAmount,
            Status = order.Status,
            TotalItems = order.OrderItems.Sum(oi => oi.Quantity),
            CreatedAt = order.CreatedAt
        };
    }
}