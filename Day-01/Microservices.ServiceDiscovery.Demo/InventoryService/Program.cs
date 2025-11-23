using Consul;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.ListenAnyIP(5004); // HTTP
});

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add Consul client
builder.Services.AddSingleton<IConsulClient>(sp => 
{
    var consulUrl = builder.Configuration.GetValue<string>("Consul:Url") ?? "http://localhost:8500";
    return new ConsulClient(config => config.Address = new Uri(consulUrl));
});

// Add health checks
builder.Services.AddHealthChecks()
    .AddCheck<InventoryServiceHealthCheck>("inventory-service-health");

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRouting();
app.UseHttpsRedirection();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

// Additional endpoints
app.MapGet("/", () => "Inventory Service is running!");
app.MapGet("/api/inventory/health", () => Results.Ok(new { status = "Healthy", service = "inventory-service" }));

// Register with Consul
try
{
    var consulClient = app.Services.GetRequiredService<IConsulClient>();
    var serviceId = $"inventory-service-{Guid.NewGuid()}";
    var servicePort = 5004;

    var registration = new AgentServiceRegistration()
    {
        ID = serviceId,
        Name = "inventory-service",
        Address = "localhost",
        Port = servicePort,
        Tags = new[] { "api", "inventory", "v1", "dotnet-10" },
        Check = new AgentServiceCheck()
        {
            HTTP = $"http://localhost:{servicePort}/health",
            Interval = TimeSpan.FromSeconds(15),
            Timeout = TimeSpan.FromSeconds(5),
            DeregisterCriticalServiceAfter = TimeSpan.FromSeconds(60)
        }
    };

    app.Logger.LogInformation("Registering inventory-service with Consul at localhost:{Port}", servicePort);
    await consulClient.Agent.ServiceRegister(registration);

    // Deregister on shutdown
    app.Lifetime.ApplicationStopping.Register(async () =>
    {
        app.Logger.LogInformation("Deregistering {ServiceId} from Consul", serviceId);
        try
        {
            await consulClient.Agent.ServiceDeregister(serviceId);
        }
        catch (Exception ex)
        {
            app.Logger.LogError(ex, "Failed to deregister service from Consul");
        }
    });
}
catch (Exception ex)
{
    app.Logger.LogError(ex, "Failed to register inventory-service with Consul");
}

app.Run();

// Health check implementation
public class InventoryServiceHealthCheck : IHealthCheck
{
    private readonly ILogger<InventoryServiceHealthCheck> _logger;

    public InventoryServiceHealthCheck(ILogger<InventoryServiceHealthCheck> logger)
    {
        _logger = logger;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("InventoryService health check executed at {Time}", DateTime.UtcNow);
            
            // Check memory usage
            var totalMemory = GC.GetTotalMemory(false) / 1024 / 1024; // MB
            
            var data = new Dictionary<string, object>
            {
                {"memory_usage_mb", totalMemory},
                {"timestamp", DateTime.UtcNow},
                {"service", "inventory-service"},
                {"version", "1.0.0"},
                {"total_items", 5}, // Simulated inventory count
                {"low_stock_items", 1} // Simulated low stock count
            };

            if (totalMemory > 500) // If using more than 500MB
            {
                return Task.FromResult(
                    HealthCheckResult.Degraded("High memory usage", data: data));
            }

            return Task.FromResult(
                HealthCheckResult.Healthy("Inventory service is healthy", data));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "InventoryService health check failed");
            return Task.FromResult(
                HealthCheckResult.Unhealthy("Inventory service health check failed", exception: ex));
        }
    }
}