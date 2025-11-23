using Consul;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel to use specific ports
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.ListenAnyIP(5002); // HTTP
});

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add Consul client with better configuration
builder.Services.AddSingleton<IConsulClient>(sp => 
{
    var config = new ConsulClientConfiguration
    {
        Address = new Uri(builder.Configuration.GetValue<string>("Consul:Url") ?? "http://localhost:8500")
    };
    return new ConsulClient(config);
});

// Add health checks with more comprehensive checks
builder.Services.AddHealthChecks()
    .AddCheck<OrderServiceHealthCheck>("order-service-health");
    //.AddUrlGroup(new Uri("http://localhost:5002/health"), "self-check"); // Self-check

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

app.UseEndpoints(endpoints =>
{
    endpoints.MapControllers();
    endpoints.MapHealthChecks("/health");
    endpoints.MapGet("/", () => "Order Service is running!");
});

// Service registration with Consul
try
{
    var consulClient = app.Services.GetRequiredService<IConsulClient>();
    var serviceId = $"order-service-{Guid.NewGuid()}";
    var servicePort = 5002;

    var registration = new AgentServiceRegistration()
    {
        ID = serviceId,
        Name = "order-service",
        Address = "localhost",
        Port = servicePort,
        Tags = new[] { "api", "orders", "v1", "dotnet-10" },
        Check = new AgentServiceCheck()
        {
            HTTP = $"http://localhost:{servicePort}/health",
            Interval = TimeSpan.FromSeconds(15),
            Timeout = TimeSpan.FromSeconds(5),
            DeregisterCriticalServiceAfter = TimeSpan.FromSeconds(60)
        }
    };

    app.Logger.LogInformation("Registering order-service with Consul at localhost:{Port}", servicePort);
    await consulClient.Agent.ServiceRegister(registration);

    // Handle graceful shutdown
    var lifetime = app.Lifetime;
    lifetime.ApplicationStopping.Register(async () =>
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
    app.Logger.LogError(ex, "Failed to register with Consul");
}

app.Run();

// Enhanced health check implementation
public class OrderServiceHealthCheck : IHealthCheck
{
    private readonly ILogger<OrderServiceHealthCheck> _logger;

    public OrderServiceHealthCheck(ILogger<OrderServiceHealthCheck> logger)
    {
        _logger = logger;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Health check executed at {Time}", DateTime.UtcNow);
            
            // Check memory usage (example health check)
            var memoryInfo = GC.GetGCMemoryInfo();
            var totalMemory = GC.GetTotalMemory(false) / 1024 / 1024; // MB
            
            var data = new Dictionary<string, object>
            {
                {"memory_usage_mb", totalMemory},
                {"gc_collections", GC.CollectionCount(0)},
                {"timestamp", DateTime.UtcNow}
            };

            if (totalMemory > 500) // If using more than 500MB
            {
                return Task.FromResult(
                    HealthCheckResult.Degraded("High memory usage", data: data));
            }

            return Task.FromResult(
                HealthCheckResult.Healthy("Service is healthy", data));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed");
            return Task.FromResult(
                HealthCheckResult.Unhealthy("Health check failed", exception: ex));
        }
    }
}