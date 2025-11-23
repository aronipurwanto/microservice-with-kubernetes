using Consul;
using Polly;
using Polly.Extensions.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.ListenAnyIP(5000); // HTTP
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

// Add HTTP client with resilience - FIXED
builder.Services.AddHttpClient("ConsulService", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});
//.AddPolicyHandler(GetRetryPolicy())
//.AddPolicyHandler(GetCircuitBreakerPolicy());

// Add health checks - FIXED (removed AddUrlGroup)
builder.Services.AddHealthChecks()
    .AddCheck<ApiGatewayHealthCheck>("api-gateway-health");

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

// Service discovery endpoints
app.MapGet("/", () => "API Gateway Service Discovery Demo");
app.MapGet("/service-discovery/demo", () => Results.Ok("Service Discovery Demo API Gateway is running!"));

app.MapGet("/service-discovery/services", async (IConsulClient consulClient) =>
{
    try
    {
        var services = await consulClient.Catalog.Services();
        return Results.Ok(new
        {
            timestamp = DateTime.UtcNow,
            totalServices = services.Response.Count,
            services = services.Response.Keys
        });
    }
    catch (Exception ex)
    {
        return Results.Problem($"Failed to retrieve services: {ex.Message}");
    }
});

app.MapGet("/service-discovery/healthy-services", async (IConsulClient consulClient) =>
{
    try
    {
        var servicesResponse = await consulClient.Catalog.Services();
        var services = servicesResponse.Response;
        
        var healthyServices = new Dictionary<string, object>();
        
        foreach (var serviceName in services.Keys)
        {
            try
            {
                var healthResponse = await consulClient.Health.Service(serviceName, default);
                healthyServices[serviceName] = new
                {
                    totalInstances = healthResponse.Response.Length,
                    instances = healthResponse.Response.Select(i => new
                    {
                        i.Service.ID,
                        i.Service.Address,
                        i.Service.Port,
                        i.Service.Tags
                    })
                };
            }
            catch (Exception ex)
            {
                healthyServices[serviceName] = new { error = ex.Message };
            }
        }
        
        return Results.Ok(new
        {
            timestamp = DateTime.UtcNow,
            healthyServices
        });
    }
    catch (Exception ex)
    {
        return Results.Problem($"Failed to retrieve healthy services: {ex.Message}");
    }
});

// Optional: Register API Gateway itself with Consul
try
{
    var consulClient = app.Services.GetRequiredService<IConsulClient>();
    var serviceId = $"api-gateway-{Guid.NewGuid()}";
    
    var registration = new AgentServiceRegistration()
    {
        ID = serviceId,
        Name = "api-gateway",
        Address = "localhost",
        Port = 5000,
        Tags = new[] { "gateway", "api", "dotnet-10" },
        Check = new AgentServiceCheck()
        {
            HTTP = "http://localhost:5000/health",
            Interval = TimeSpan.FromSeconds(15),
            Timeout = TimeSpan.FromSeconds(5),
            DeregisterCriticalServiceAfter = TimeSpan.FromSeconds(30)
        }
    };

    app.Logger.LogInformation("Registering API Gateway with Consul");
    await consulClient.Agent.ServiceRegister(registration);

    // Deregister on shutdown
    app.Lifetime.ApplicationStopping.Register(async () =>
    {
        app.Logger.LogInformation("Deregistering API Gateway from Consul");
        await consulClient.Agent.ServiceDeregister(serviceId);
    });
}
catch (Exception ex)
{
    app.Logger.LogError(ex, "Failed to register API Gateway with Consul");
}

app.Run();

// Polly resilience policies - Make sure these are static
static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
{
    return HttpPolicyExtensions
        .HandleTransientHttpError()
        .Or<TimeoutException>()
        .WaitAndRetryAsync(
            retryCount: 3,
            sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
            onRetry: (outcome, timespan, retryCount, context) =>
            {
                Console.WriteLine($"Retry {retryCount} after {timespan.TotalSeconds}s for {outcome?.Exception?.Message}");
            });
}

static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
{
    return HttpPolicyExtensions
        .HandleTransientHttpError()
        .Or<TimeoutException>()
        .CircuitBreakerAsync(
            handledEventsAllowedBeforeBreaking: 3,
            durationOfBreak: TimeSpan.FromSeconds(60),
            onBreak: (outcome, breakDelay) =>
            {
                Console.WriteLine($"Circuit breaker opened for {breakDelay.TotalSeconds}s");
            },
            onReset: () =>
            {
                Console.WriteLine("Circuit breaker reset");
            });
}

// Health check implementation
public class ApiGatewayHealthCheck : IHealthCheck
{
    private readonly IConsulClient _consulClient;
    private readonly ILogger<ApiGatewayHealthCheck> _logger;

    public ApiGatewayHealthCheck(IConsulClient consulClient, ILogger<ApiGatewayHealthCheck> logger)
    {
        _consulClient = consulClient;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Test Consul connection
            var leader = await _consulClient.Status.Leader();
            
            var data = new Dictionary<string, object>
            {
                {"consul_connected", true},
                {"timestamp", DateTime.UtcNow},
                {"service", "api-gateway"}
            };

            return HealthCheckResult.Healthy("API Gateway is healthy", data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed");
            return HealthCheckResult.Unhealthy("Cannot connect to Consul", ex);
        }
    }
}