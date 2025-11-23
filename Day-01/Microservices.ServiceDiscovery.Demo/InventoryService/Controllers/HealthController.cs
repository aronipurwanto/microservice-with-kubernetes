using Microsoft.AspNetCore.Mvc;

namespace InventoryService.Controllers;

[ApiController]
[Route("[controller]")]
public class HealthController : ControllerBase
{
    private readonly ILogger<HealthController> _logger;

    public HealthController(ILogger<HealthController> logger)
    {
        _logger = logger;
    }

    [HttpGet]
    public IActionResult HealthCheck()
    {
        _logger.LogInformation("Health check requested for InventoryService");
        return Ok(new { 
            status = "Healthy", 
            timestamp = DateTime.UtcNow,
            service = "inventory-service",
            version = "1.0.0"
        });
    }

    [HttpGet("detailed")]
    public IActionResult DetailedHealth()
    {
        _logger.LogInformation("Detailed health check requested for InventoryService");
        
        var memoryInfo = GC.GetGCMemoryInfo();
        var totalMemory = GC.GetTotalMemory(false) / 1024 / 1024; // MB
        
        return Ok(new
        {
            status = "Healthy",
            timestamp = DateTime.UtcNow,
            service = "inventory-service",
            version = "1.0.0",
            memory = new { 
                usage_mb = totalMemory,
                max_memory_mb = memoryInfo.HighMemoryLoadThresholdBytes / 1024 / 1024
            },
            dependencies = new { 
                database = "Connected (simulated)",
                consul = "Connected"
            },
            inventory = new {
                total_items = 5,
                total_value = 1250.75m,
                low_stock_items = 1,
                categories = new[] { "ELECTRONICS", "FURNITURE", "STATIONERY" }
            },
            metrics = new {
                gc_collections = GC.CollectionCount(0)
            }
        });
    }

    [HttpGet("ready")]
    public IActionResult ReadinessCheck()
    {
        _logger.LogInformation("Readiness check requested for InventoryService");
        
        // Simulate readiness checks
        var isReady = true; // In real app, check actual dependencies
        
        if (isReady)
        {
            return Ok(new { 
                status = "Ready", 
                timestamp = DateTime.UtcNow,
                message = "InventoryService is ready to accept requests"
            });
        }
        
        return StatusCode(503, new { 
            status = "Not Ready", 
            timestamp = DateTime.UtcNow,
            message = "InventoryService is not ready to accept requests"
        });
    }
}