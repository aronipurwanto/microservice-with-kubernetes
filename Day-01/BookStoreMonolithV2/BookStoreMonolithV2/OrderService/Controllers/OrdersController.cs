using Microsoft.AspNetCore.Mvc;
using OrderService.Services;
using SharedModels.Models;

namespace OrderService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;

    public OrdersController(IOrderService orderService)
    {
        _orderService = orderService;
    }

    [HttpPost]
    public async Task<ActionResult<OrderResponseDto>> CreateOrder(OrderCreateDto orderCreateDto)
    {
        var order = await _orderService.CreateOrderAsync(orderCreateDto);
        return CreatedAtAction(nameof(GetOrder), new { id = order.Id }, order);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<OrderResponseDto>> GetOrder(Guid id)
    {
        var order = await _orderService.GetOrderAsync(id);
        if (order == null)
        {
            return NotFound();
        }
        return order;
    }

    [HttpGet("user/{userId}")]
    public async Task<ActionResult<List<OrderSummaryDto>>> GetUserOrders(Guid userId)
    {
        var orders = await _orderService.GetUserOrdersAsync(userId);
        return orders;
    }

    [HttpPut("{id}/status")]
    public async Task<ActionResult<OrderResponseDto>> UpdateOrderStatus(Guid id, UpdateOrderStatusDto updateDto)
    {
        var order = await _orderService.UpdateOrderStatusAsync(id, updateDto);
        if (order == null)
        {
            return NotFound();
        }
        return order;
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteOrder(Guid id)
    {
        var result = await _orderService.DeleteOrderAsync(id);
        if (!result)
        {
            return NotFound();
        }
        return NoContent();
    }
}