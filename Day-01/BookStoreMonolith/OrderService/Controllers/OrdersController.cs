using Microsoft.AspNetCore.Mvc;
using OrderService.Services;
using SharedModels.Models;

namespace OrderService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;
    private readonly ILogger<OrdersController> _logger;

    public OrdersController(IOrderService orderService, ILogger<OrdersController> logger)
    {
        _orderService = orderService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<List<OrderSummaryDto>>> GetOrders(
        [FromQuery] int page = 1, 
        [FromQuery] int pageSize = 20)
    {
        try
        {
            var orders = await _orderService.GetOrdersAsync(page, pageSize);
            return Ok(orders);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving orders");
            return StatusCode(500, "An error occurred while retrieving orders");
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OrderResponseDto>> GetOrder(Guid id)
    {
        try
        {
            var order = await _orderService.GetOrderAsync(id);
            if (order == null)
            {
                return NotFound($"Order with ID {id} not found");
            }
            return Ok(order);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving order with ID: {OrderId}", id);
            return StatusCode(500, "An error occurred while retrieving the order");
        }
    }

    [HttpPost]
    public async Task<ActionResult<OrderResponseDto>> CreateOrder(OrderCreateDto orderDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (orderDto.OrderItems == null || !orderDto.OrderItems.Any())
            {
                return BadRequest("Order must contain at least one item");
            }

            var order = await _orderService.CreateOrderAsync(orderDto);
            return CreatedAtAction(nameof(GetOrder), new { id = order.Id }, order);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating order");
            return StatusCode(500, "An error occurred while creating the order");
        }
    }

    [HttpPut("{id:guid}/status")]
    public async Task<IActionResult> UpdateOrderStatus(
        Guid id, 
        [FromBody] UpdateOrderStatusDto statusDto)
    {
        try
        {
            var success = await _orderService.UpdateOrderStatusAsync(id, statusDto);
            if (!success)
            {
                return NotFound($"Order with ID {id} not found");
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating order status with ID: {OrderId}", id);
            return StatusCode(500, "An error occurred while updating the order status");
        }
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> CancelOrder(
        Guid id, 
        [FromBody] string? reason = null)
    {
        try
        {
            var success = await _orderService.CancelOrderAsync(id, reason);
            if (!success)
            {
                return NotFound($"Order with ID {id} not found");
            }

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling order with ID: {OrderId}", id);
            return StatusCode(500, "An error occurred while cancelling the order");
        }
    }

    [HttpGet("user/{userId:guid}")]
    public async Task<ActionResult<List<OrderResponseDto>>> GetOrdersByUser(Guid userId)
    {
        try
        {
            var orders = await _orderService.GetOrdersByUserAsync(userId);
            return Ok(orders);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving orders for user ID: {UserId}", userId);
            return StatusCode(500, "An error occurred while retrieving orders");
        }
    }

    [HttpGet("status/{status}")]
    public async Task<ActionResult<List<OrderResponseDto>>> GetOrdersByStatus(OrderStatus status)
    {
        try
        {
            var orders = await _orderService.GetOrdersByStatusAsync(status);
            return Ok(orders);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving orders with status: {Status}", status);
            return StatusCode(500, "An error occurred while retrieving orders");
        }
    }
}