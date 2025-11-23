using Microsoft.EntityFrameworkCore;
using PaymentService.Models;
using PaymentService.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SharedModels.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure Entity Framework with SQL Server
builder.Services.AddDbContext<PaymentContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Register services
builder.Services.AddScoped<IPaymentService, PaymentService.Services.PaymentService>();

// Add Health Checks
builder.Services.AddHealthChecks()
    .AddCheck<PaymentContextHealthCheck>("PaymentService-DB");

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
    app.UseSwagger();
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
        var context = services.GetRequiredService<PaymentContext>();
        
        // Use EnsureCreatedAsync for async operation
        var created = await context.Database.EnsureCreatedAsync();
        if (created)
        {
            Console.WriteLine("PaymentService database created successfully.");
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

static async Task SeedSampleData(PaymentContext context)
{
    try
    {
        // Check if any payments already exist
        if (!await context.Payments.AnyAsync())
        {
            Console.WriteLine("Seeding PaymentService sample data...");

            // Create sample payment 1
            var paymentId1 = Guid.NewGuid();
            var payment1 = new Payment
            {
                Id = paymentId1,
                OrderId = Guid.NewGuid(),
                Amount = 149.97m,
                Currency = "USD",
                PaymentDate = DateTime.UtcNow.AddDays(-1),
                Status = PaymentStatus.Completed,
                PaymentMethod = "CreditCard",
                GatewayTransactionId = $"AUTH_{DateTime.UtcNow.Ticks}",
                Description = "Payment for order #001",
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            };

            var transaction1 = new Transaction
            {
                Id = Guid.NewGuid(),
                PaymentId = paymentId1,
                Amount = 149.97m,
                TransactionType = "Authorization",
                GatewayTransactionId = payment1.GatewayTransactionId,
                CreatedAt = DateTime.UtcNow.AddDays(-1),
                Status = "Completed",
                GatewayResponse = "Approved"
            };

            payment1.Transactions.Add(transaction1);
            await context.Payments.AddAsync(payment1);

            // Create sample payment 2
            var paymentId2 = Guid.NewGuid();
            var payment2 = new Payment
            {
                Id = paymentId2,
                OrderId = Guid.NewGuid(),
                Amount = 75.50m,
                Currency = "USD",
                PaymentDate = DateTime.UtcNow.AddDays(-2),
                Status = PaymentStatus.Refunded,
                PaymentMethod = "PayPal",
                GatewayTransactionId = $"AUTH_{DateTime.UtcNow.Ticks - 1000}",
                Description = "Payment for order #002",
                CreatedAt = DateTime.UtcNow.AddDays(-2)
            };

            var transaction2 = new Transaction
            {
                Id = Guid.NewGuid(),
                PaymentId = paymentId2,
                Amount = 75.50m,
                TransactionType = "Authorization",
                GatewayTransactionId = payment2.GatewayTransactionId,
                CreatedAt = DateTime.UtcNow.AddDays(-2),
                Status = "Completed",
                GatewayResponse = "Approved"
            };

            var refundTransaction = new Transaction
            {
                Id = Guid.NewGuid(),
                PaymentId = paymentId2,
                Amount = 75.50m,
                TransactionType = "Refund",
                GatewayTransactionId = $"REFUND_{DateTime.UtcNow.Ticks}",
                CreatedAt = DateTime.UtcNow.AddDays(-1),
                Status = "Completed",
                GatewayResponse = "Refund processed successfully"
            };

            payment2.Transactions.Add(transaction2);
            payment2.Transactions.Add(refundTransaction);
            await context.Payments.AddAsync(payment2);

            // Create sample payment 3 (failed payment)
            var paymentId3 = Guid.NewGuid();
            var payment3 = new Payment
            {
                Id = paymentId3,
                OrderId = Guid.NewGuid(),
                Amount = 200.00m,
                Currency = "USD",
                PaymentDate = DateTime.UtcNow.AddDays(-1),
                Status = PaymentStatus.Failed,
                PaymentMethod = "CreditCard",
                GatewayTransactionId = $"AUTH_{DateTime.UtcNow.Ticks - 500}",
                Description = "Payment for order #003 - Insufficient funds",
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            };

            var failedTransaction = new Transaction
            {
                Id = Guid.NewGuid(),
                PaymentId = paymentId3,
                Amount = 200.00m,
                TransactionType = "Authorization",
                GatewayTransactionId = payment3.GatewayTransactionId,
                CreatedAt = DateTime.UtcNow.AddDays(-1),
                Status = "Failed",
                GatewayResponse = "Insufficient funds",
                ErrorMessage = "Card declined: insufficient funds"
            };

            payment3.Transactions.Add(failedTransaction);
            await context.Payments.AddAsync(payment3);

            var recordsAffected = await context.SaveChangesAsync();
            Console.WriteLine($"PaymentService sample data seeded successfully. {recordsAffected} records added.");
        }
        else
        {
            Console.WriteLine("PaymentService database already has data, skipping seed.");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error during PaymentService data seeding: {ex.Message}");
        if (ex.InnerException != null)
        {
            Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
        }
        throw;
    }
}

// Custom Health Check Class
public class PaymentContextHealthCheck : IHealthCheck
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PaymentContextHealthCheck> _logger;

    public PaymentContextHealthCheck(IServiceProvider serviceProvider, ILogger<PaymentContextHealthCheck> logger)
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
            var dbContext = scope.ServiceProvider.GetRequiredService<PaymentContext>();
            
            _logger.LogInformation("Checking PaymentService database health...");
            var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);
            
            if (canConnect)
            {
                _logger.LogInformation("PaymentService database health check passed");
                return HealthCheckResult.Healthy("Database connection is OK");
            }
            else
            {
                _logger.LogWarning("PaymentService database health check failed - cannot connect");
                return HealthCheckResult.Unhealthy("Cannot connect to database");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PaymentService database health check failed with exception");
            return HealthCheckResult.Unhealthy($"Database check failed: {ex.Message}");
        }
    }
}

// Seed sample data method for PaymentService