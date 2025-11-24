using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient();

// Service registry (in-memory)
var serviceRegistry = new Dictionary<string, List<string>>
{
    { "ProductService", new List<string> { "http://localhost:5001", "https://localhost:5002" } },
    { "OrderService", new List<string> { "http://localhost:6001", "https://localhost:6002" } }
};

var app = builder.Build();

// Configure HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Service Discovery Endpoints
app.MapGet("/discovery/services", () =>
{
    var services = serviceRegistry.Keys.Select(serviceName => new
    {
        Name = serviceName,
        Instances = serviceRegistry[serviceName].Count,
        Urls = serviceRegistry[serviceName]
    });
    
    return Results.Ok(services);
})
.WithName("GetAllServices")
.WithOpenApi();

app.MapGet("/discovery/services/{serviceName}", (string serviceName) =>
{
    if (serviceRegistry.ContainsKey(serviceName))
    {
        return Results.Ok(new
        {
            Service = serviceName,
            Instances = serviceRegistry[serviceName],
            Count = serviceRegistry[serviceName].Count
        });
    }
    
    return Results.NotFound($"Service '{serviceName}' not found");
})
.WithName("GetServiceInstances")
.WithOpenApi();

// Gateway Routing
app.MapGet("/gateway/products", async (IHttpClientFactory httpClientFactory) =>
{
    try
    {
        var client = httpClientFactory.CreateClient();
        var serviceUrl = serviceRegistry["ProductService"].First();
        var response = await client.GetAsync($"{serviceUrl}/products");
        
        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            return Results.Ok(JsonSerializer.Deserialize<object>(content));
        }
        
        return Results.StatusCode((int)response.StatusCode);
    }
    catch (Exception ex)
    {
        return Results.Problem($"Error calling ProductService: {ex.Message}");
    }
})
.WithName("GatewayGetProducts")
.WithOpenApi();

app.MapGet("/gateway/orders", async (IHttpClientFactory httpClientFactory) =>
{
    try
    {
        var client = httpClientFactory.CreateClient();
        var serviceUrl = serviceRegistry["OrderService"].First();
        var response = await client.GetAsync($"{serviceUrl}/orders");
        
        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            return Results.Ok(JsonSerializer.Deserialize<object>(content));
        }
        
        return Results.StatusCode((int)response.StatusCode);
    }
    catch (Exception ex)
    {
        return Results.Problem($"Error calling OrderService: {ex.Message}");
    }
})
.WithName("GatewayGetOrders")
.WithOpenApi();

// Health Check Aggregator
app.MapGet("/gateway/health", async (IHttpClientFactory httpClientFactory) =>
{
    var healthResults = new Dictionary<string, object>();
    var client = httpClientFactory.CreateClient();
    
    foreach (var service in serviceRegistry)
    {
        try
        {
            var serviceUrl = service.Value.First();
            var response = await client.GetAsync($"{serviceUrl}/");
            
            healthResults[service.Key] = new
            {
                Status = response.IsSuccessStatusCode ? "Healthy" : "Unhealthy",
                StatusCode = (int)response.StatusCode,
                Timestamp = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            healthResults[service.Key] = new
            {
                Status = "Unreachable",
                Error = ex.Message,
                Timestamp = DateTime.UtcNow
            };
        }
    }
    
    return Results.Ok(healthResults);
})
.WithName("GatewayHealthCheck")
.WithOpenApi();

app.MapGet("/", () => "Service Discovery API Gateway is running!")
    .WithName("GatewayRoot")
    .WithOpenApi();

app.Run();