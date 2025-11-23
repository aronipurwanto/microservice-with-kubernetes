using Microsoft.AspNetCore.Mvc;

namespace OrderService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private static readonly List<Order> _orders = new()
    {
        new Order(1, "Pending", "customer-1", 99.99m, DateTime.UtcNow.AddDays(-1)),
        new Order(2, "Completed", "customer-2", 149.99m, DateTime.UtcNow.AddDays(-2)),
        new Order(3, "Processing", "customer-1", 199.99m, DateTime.UtcNow.AddHours(-2))
    };

    private readonly ILogger<OrdersController> _logger;

    public OrdersController(ILogger<OrdersController> logger)
    {
        _logger = logger;
    }

    [HttpGet]
    public ActionResult<IEnumerable<Order>> GetOrders()
    {
        _logger.LogInformation("Retrieving all orders. Total: {OrderCount}", _orders.Count);
        return Ok(_orders);
    }

    [HttpGet("{id}")]
    public ActionResult<Order> GetOrder(int id)
    {
        _logger.LogInformation("Retrieving order with ID: {OrderId}", id);
        
        var order = _orders.FirstOrDefault(o => o.Id == id);
        if (order == null)
        {
            _logger.LogWarning("Order with ID {OrderId} not found", id);
            return NotFound(new { error = $"Order with ID {id} not found" });
        }
        
        return Ok(order);
    }

    [HttpPost]
    public ActionResult<Order> CreateOrder(CreateOrderRequest request)
    {
        _logger.LogInformation("Creating new order for customer: {CustomerId}", request.CustomerId);
        
        var newOrder = new Order(
            _orders.Count + 1,
            "Pending",
            request.CustomerId,
            request.TotalAmount,
            DateTime.UtcNow
        );

        _orders.Add(newOrder);
        
        _logger.LogInformation("Order created with ID: {OrderId}", newOrder.Id);
        
        return CreatedAtAction(nameof(GetOrder), new { id = newOrder.Id }, newOrder);
    }

    [HttpGet("customer/{customerId}")]
    public ActionResult<IEnumerable<Order>> GetCustomerOrders(string customerId)
    {
        _logger.LogInformation("Retrieving orders for customer: {CustomerId}", customerId);
        
        var customerOrders = _orders.Where(o => o.CustomerId == customerId).ToList();
        
        return Ok(new
        {
            customerId,
            totalOrders = customerOrders.Count,
            orders = customerOrders
        });
    }
}

public record Order(int Id, string Status, string CustomerId, decimal TotalAmount, DateTime CreatedAt);
public record CreateOrderRequest(string CustomerId, decimal TotalAmount);