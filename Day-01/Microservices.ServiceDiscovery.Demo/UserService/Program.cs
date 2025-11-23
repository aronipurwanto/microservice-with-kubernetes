using Consul;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.ListenAnyIP(5003); // HTTP
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
    .AddCheck<UserServiceHealthCheck>("user-service-health");

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
app.MapGet("/", () => "User Service is running!");
app.MapGet("/api/users/health", () => Results.Ok(new { status = "Healthy", service = "user-service" }));

// Register with Consul
try
{
    var consulClient = app.Services.GetRequiredService<IConsulClient>();
    var serviceId = $"user-service-{Guid.NewGuid()}";
    var servicePort = 5003;

    var registration = new AgentServiceRegistration()
    {
        ID = serviceId,
        Name = "user-service",
        Address = "localhost",
        Port = servicePort,
        Tags = new[] { "api", "users", "v1", "dotnet-10" },
        Check = new AgentServiceCheck()
        {
            HTTP = $"http://localhost:{servicePort}/health",
            Interval = TimeSpan.FromSeconds(15),
            Timeout = TimeSpan.FromSeconds(5),
            DeregisterCriticalServiceAfter = TimeSpan.FromSeconds(60)
        }
    };

    app.Logger.LogInformation("Registering user-service with Consul at localhost:{Port}", servicePort);
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
    app.Logger.LogError(ex, "Failed to register user-service with Consul");
}

app.Run();

// Health check implementation
public class UserServiceHealthCheck : IHealthCheck
{
    private readonly ILogger<UserServiceHealthCheck> _logger;

    public UserServiceHealthCheck(ILogger<UserServiceHealthCheck> logger)
    {
        _logger = logger;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("UserService health check executed at {Time}", DateTime.UtcNow);
            
            // Check memory usage
            var totalMemory = GC.GetTotalMemory(false) / 1024 / 1024; // MB
            
            var data = new Dictionary<string, object>
            {
                {"memory_usage_mb", totalMemory},
                {"timestamp", DateTime.UtcNow},
                {"service", "user-service"},
                {"version", "1.0.0"}
            };

            if (totalMemory > 500) // If using more than 500MB
            {
                return Task.FromResult(
                    HealthCheckResult.Degraded("High memory usage", data: data));
            }

            return Task.FromResult(
                HealthCheckResult.Healthy("User service is healthy", data));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UserService health check failed");
            return Task.FromResult(
                HealthCheckResult.Unhealthy("User service health check failed", exception: ex));
        }
    }
}