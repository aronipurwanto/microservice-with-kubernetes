using InventoryService.Models;
using Microsoft.AspNetCore.Mvc;

namespace InventoryService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InventoryController : ControllerBase
{
    private static readonly List<InventoryItem> _inventory = new()
    {
        new InventoryItem(1, "Laptop", "ELECTRONICS", 25, 10, 499.99m, DateTime.UtcNow.AddDays(-30)),
        new InventoryItem(2, "Office Chair", "FURNITURE", 50, 5, 199.99m, DateTime.UtcNow.AddDays(-15)),
        new InventoryItem(3, "Notebook", "STATIONERY", 200, 50, 4.99m, DateTime.UtcNow.AddDays(-7)),
        new InventoryItem(4, "Monitor", "ELECTRONICS", 30, 8, 299.99m, DateTime.UtcNow.AddDays(-20)),
        new InventoryItem(5, "Pen", "STATIONERY", 500, 100, 1.99m, DateTime.UtcNow.AddDays(-5))
    };

    private readonly ILogger<InventoryController> _logger;

    public InventoryController(ILogger<InventoryController> logger)
    {
        _logger = logger;
    }

    [HttpGet]
    public ActionResult<IEnumerable<InventoryItem>> GetInventory()
    {
        _logger.LogInformation("Retrieving all inventory items. Total: {ItemCount}", _inventory.Count);
        return Ok(new
        {
            totalItems = _inventory.Count,
            totalValue = _inventory.Sum(i => i.Quantity * i.Price),
            items = _inventory
        });
    }

    [HttpGet("{id}")]
    public ActionResult<InventoryItem> GetInventoryItem(int id)
    {
        _logger.LogInformation("Retrieving inventory item with ID: {ItemId}", id);
        
        var item = _inventory.FirstOrDefault(i => i.Id == id);
        if (item == null)
        {
            _logger.LogWarning("Inventory item with ID {ItemId} not found", id);
            return NotFound(new { error = $"Inventory item with ID {id} not found" });
        }
        
        return Ok(item);
    }

    [HttpGet("category/{category}")]
    public ActionResult<IEnumerable<InventoryItem>> GetItemsByCategory(string category)
    {
        _logger.LogInformation("Retrieving inventory items for category: {Category}", category);
        
        var items = _inventory.Where(i => 
            string.Equals(i.Category, category, StringComparison.OrdinalIgnoreCase)).ToList();
        
        return Ok(new
        {
            category = category,
            totalItems = items.Count,
            totalValue = items.Sum(i => i.Quantity * i.Price),
            items = items
        });
    }

    [HttpGet("low-stock")]
    public ActionResult<IEnumerable<InventoryItem>> GetLowStockItems()
    {
        _logger.LogInformation("Retrieving low stock inventory items");
        
        var lowStockItems = _inventory.Where(i => i.Quantity <= i.ReorderLevel).ToList();
        
        return Ok(new
        {
            totalLowStockItems = lowStockItems.Count,
            items = lowStockItems
        });
    }

    [HttpPost]
    public ActionResult<InventoryItem> CreateInventoryItem(CreateInventoryItemRequest request)
    {
        _logger.LogInformation("Creating new inventory item: {ItemName}", request.Name);
        
        // Validate name uniqueness
        if (_inventory.Any(i => string.Equals(i.Name, request.Name, StringComparison.OrdinalIgnoreCase)))
        {
            _logger.LogWarning("Inventory item with name {ItemName} already exists", request.Name);
            return Conflict(new { error = $"Inventory item with name {request.Name} already exists" });
        }

        var newItem = new InventoryItem(
            _inventory.Count + 1,
            request.Name,
            request.Category,
            request.Quantity,
            request.ReorderLevel,
            request.Price,
            DateTime.UtcNow
        );

        _inventory.Add(newItem);
        
        _logger.LogInformation("Inventory item created with ID: {ItemId}", newItem.Id);
        
        return CreatedAtAction(nameof(GetInventoryItem), new { id = newItem.Id }, newItem);
    }

    [HttpPut("{id}")]
    public ActionResult<InventoryItem> UpdateInventoryItem(int id, UpdateInventoryItemRequest request)
    {
        _logger.LogInformation("Updating inventory item with ID: {ItemId}", id);
        
        var existingItem = _inventory.FirstOrDefault(i => i.Id == id);
        if (existingItem == null)
        {
            _logger.LogWarning("Inventory item with ID {ItemId} not found for update", id);
            return NotFound(new { error = $"Inventory item with ID {id} not found" });
        }

        var updatedItem = existingItem with 
        { 
            Name = request.Name ?? existingItem.Name,
            Category = request.Category ?? existingItem.Category,
            Quantity = request.Quantity ?? existingItem.Quantity,
            ReorderLevel = request.ReorderLevel ?? existingItem.ReorderLevel,
            Price = request.Price ?? existingItem.Price
        };

        _inventory.Remove(existingItem);
        _inventory.Add(updatedItem);
        
        _logger.LogInformation("Inventory item with ID {ItemId} updated successfully", id);
        
        return Ok(updatedItem);
    }

    [HttpPatch("{id}/stock")]
    public ActionResult<InventoryItem> UpdateStockLevel(int id, UpdateStockRequest request)
    {
        _logger.LogInformation("Updating stock level for item ID: {ItemId}", id);
        
        var existingItem = _inventory.FirstOrDefault(i => i.Id == id);
        if (existingItem == null)
        {
            _logger.LogWarning("Inventory item with ID {ItemId} not found for stock update", id);
            return NotFound(new { error = $"Inventory item with ID {id} not found" });
        }

        var updatedItem = existingItem with 
        { 
            Quantity = request.Quantity
        };

        _inventory.Remove(existingItem);
        _inventory.Add(updatedItem);
        
        _logger.LogInformation("Stock level updated for item ID {ItemId}. New quantity: {Quantity}", id, request.Quantity);
        
        return Ok(updatedItem);
    }

    [HttpDelete("{id}")]
    public ActionResult DeleteInventoryItem(int id)
    {
        _logger.LogInformation("Deleting inventory item with ID: {ItemId}", id);
        
        var item = _inventory.FirstOrDefault(i => i.Id == id);
        if (item == null)
        {
            _logger.LogWarning("Inventory item with ID {ItemId} not found for deletion", id);
            return NotFound(new { error = $"Inventory item with ID {id} not found" });
        }

        _inventory.Remove(item);
        
        _logger.LogInformation("Inventory item with ID {ItemId} deleted successfully", id);
        
        return NoContent();
    }

    [HttpGet("search")]
    public ActionResult<IEnumerable<InventoryItem>> SearchInventory([FromQuery] string query)
    {
        _logger.LogInformation("Searching inventory with query: {Query}", query);
        
        if (string.IsNullOrWhiteSpace(query))
        {
            return BadRequest(new { error = "Search query is required" });
        }

        var results = _inventory.Where(i => 
            i.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            i.Category.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
        
        return Ok(new
        {
            query = query,
            totalResults = results.Count,
            items = results
        });
    }

    [HttpGet("stats")]
    public ActionResult<InventoryStats> GetInventoryStats()
    {
        _logger.LogInformation("Retrieving inventory statistics");
        
        var stats = new InventoryStats
        {
            TotalItems = _inventory.Count,
            TotalValue = _inventory.Sum(i => i.Quantity * i.Price),
            TotalQuantity = _inventory.Sum(i => i.Quantity),
            Categories = _inventory.GroupBy(i => i.Category)
                .Select(g => new CategoryStats
                {
                    Category = g.Key,
                    ItemCount = g.Count(),
                    TotalValue = g.Sum(i => i.Quantity * i.Price),
                    TotalQuantity = g.Sum(i => i.Quantity)
                }).ToList(),
            LowStockItems = _inventory.Count(i => i.Quantity <= i.ReorderLevel),
            LastUpdated = DateTime.UtcNow
        };
        
        return Ok(stats);
    }
}