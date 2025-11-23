using Microsoft.EntityFrameworkCore;
using OrderService.Models;
using OrderService.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SharedModels.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure Entity Framework with SQL Server
builder.Services.AddDbContext<OrderContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Register services
builder.Services.AddScoped<IOrderService, OrderService.Services.OrderService>();

// Add Health Checks
builder.Services.AddHealthChecks()
    .AddCheck<OrderContextHealthCheck>("OrderService-DB");

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    //app.UseSwagger(); // Uncomment this line for Swagger JSON
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");
app.UseAuthorization();

// Map controllers
app.MapControllers();

// Add health check endpoint
app.MapHealthChecks("/health");

// Initialize database
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<OrderContext>();
        
        // Use EnsureCreatedAsync for async operation
        var created = await context.Database.EnsureCreatedAsync();
        if (created)
        {
            Console.WriteLine("Database created successfully.");
        }
        
        // Seed sample data if needed
        await SeedSampleData(context);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while seeding the database.");
    }
}

app.Run();

static async Task SeedSampleData(OrderContext context)
{
    try
    {
        // Check if any orders already exist
        if (!await context.Orders.AnyAsync())
        {
            Console.WriteLine("Seeding sample data...");

            var orderId = Guid.NewGuid();
            var order = new Order
            {
                Id = orderId,
                UserId = Guid.NewGuid(),
                OrderDate = DateTime.UtcNow.AddDays(-1),
                TotalAmount = 149.97m,
                Status = OrderStatus.Confirmed,
                ShippingAddress = "123 Main St, City, Country 12345",
                CustomerNotes = "Please deliver after 5 PM",
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            };

            var orderItems = new List<OrderItem>
            {
                new OrderItem
                {
                    Id = Guid.NewGuid(),
                    OrderId = orderId,
                    ProductId = Guid.NewGuid(),
                    ProductName = "ASP.NET Core in Action",
                    ProductDescription = "Comprehensive guide to ASP.NET Core development",
                    Quantity = 2,
                    UnitPrice = 49.99m
                },
                new OrderItem
                {
                    Id = Guid.NewGuid(),
                    OrderId = orderId,
                    ProductId = Guid.NewGuid(),
                    ProductName = "Docker and Kubernetes Guide",
                    ProductDescription = "Complete guide to containerization and orchestration",
                    Quantity = 1,
                    UnitPrice = 49.99m
                }
            };

            order.OrderItems.AddRange(orderItems);
            await context.Orders.AddAsync(order);

            // Add a second sample order with different status
            var orderId2 = Guid.NewGuid();
            var order2 = new Order
            {
                Id = orderId2,
                UserId = Guid.NewGuid(),
                OrderDate = DateTime.UtcNow.AddDays(-2),
                TotalAmount = 75.50m,
                Status = OrderStatus.Delivered,
                ShippingAddress = "456 Oak Avenue, Town, Country 67890",
                CreatedAt = DateTime.UtcNow.AddDays(-2)
            };

            var orderItems2 = new List<OrderItem>
            {
                new OrderItem
                {
                    Id = Guid.NewGuid(),
                    OrderId = orderId2,
                    ProductId = Guid.NewGuid(),
                    ProductName = "Microservices Patterns",
                    ProductDescription = "Learn microservices architecture patterns",
                    Quantity = 1,
                    UnitPrice = 75.50m
                }
            };

            order2.OrderItems.AddRange(orderItems2);
            await context.Orders.AddAsync(order2);

            var recordsAffected = await context.SaveChangesAsync();
            Console.WriteLine($"Sample data seeded successfully. {recordsAffected} records added.");
        }
        else
        {
            Console.WriteLine("Database already has data, skipping seed.");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error during data seeding: {ex.Message}");
        if (ex.InnerException != null)
        {
            Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
        }
        throw;
    }
}

// Custom Health Check Class
public class OrderContextHealthCheck : IHealthCheck
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OrderContextHealthCheck> _logger;

    public OrderContextHealthCheck(IServiceProvider serviceProvider, ILogger<OrderContextHealthCheck> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<OrderContext>();
            
            _logger.LogInformation("Checking database health...");
            var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);
            
            if (canConnect)
            {
                _logger.LogInformation("Database health check passed");
                return HealthCheckResult.Healthy("Database connection is OK");
            }
            else
            {
                _logger.LogWarning("Database health check failed - cannot connect");
                return HealthCheckResult.Unhealthy("Cannot connect to database");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database health check failed with exception");
            return HealthCheckResult.Unhealthy($"Database check failed: {ex.Message}");
        }
    }
}

// Seed sample data method - FIXED VERSION