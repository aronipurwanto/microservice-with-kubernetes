namespace InventoryService.Models;


// Inventory models and DTOs
public record InventoryItem(
    int Id, 
    string Name, 
    string Category, 
    int Quantity, 
    int ReorderLevel, 
    decimal Price, 
    DateTime LastUpdated);

public record CreateInventoryItemRequest(
    string Name, 
    string Category, 
    int Quantity, 
    int ReorderLevel, 
    decimal Price);

public record UpdateInventoryItemRequest(
    string? Name, 
    string? Category, 
    int? Quantity, 
    int? ReorderLevel, 
    decimal? Price);

public record UpdateStockRequest(int Quantity);

public record InventoryStats
{
    public int TotalItems { get; init; }
    public decimal TotalValue { get; init; }
    public int TotalQuantity { get; init; }
    public List<CategoryStats> Categories { get; init; } = new();
    public int LowStockItems { get; init; }
    public DateTime LastUpdated { get; init; }
}

public record CategoryStats
{
    public string Category { get; init; } = string.Empty;
    public int ItemCount { get; init; }
    public decimal TotalValue { get; init; }
    public int TotalQuantity { get; init; }
}