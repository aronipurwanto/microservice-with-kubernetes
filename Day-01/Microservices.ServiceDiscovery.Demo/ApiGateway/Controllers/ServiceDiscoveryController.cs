using Consul;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace ApiGateway.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ServiceDiscoveryController : ControllerBase
{
    private readonly IConsulClient _consulClient;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ServiceDiscoveryController> _logger;
    private static long _requestCount = 0;

    public ServiceDiscoveryController(
        IConsulClient consulClient,
        IHttpClientFactory httpClientFactory,
        ILogger<ServiceDiscoveryController> logger)
    {
        _consulClient = consulClient;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    [HttpGet("services")]
    public async Task<IActionResult> GetRegisteredServices()
    {
        try
        {
            var services = await _consulClient.Catalog.Services();
            return Ok(new
            {
                timestamp = DateTime.UtcNow,
                totalServices = services.Response.Count,
                services = services.Response.Keys
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve services from Consul");
            return StatusCode(500, new { error = "Failed to retrieve services" });
        }
    }

    [HttpGet("services/{serviceName}")]
    public async Task<IActionResult> GetServiceInstances(string serviceName)
    {
        try
        {
            var services = await _consulClient.Catalog.Service(serviceName);
            var instances = services.Response;

            return Ok(new
            {
                serviceName,
                timestamp = DateTime.UtcNow,
                totalInstances = instances.Length,
                instances = instances.Select(i => new
                {
                    i.ServiceID,
                    i.ServiceName,
                    i.ServiceAddress,
                    i.ServicePort,
                    i.ServiceTags
                })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve service instances for {ServiceName}", serviceName);
            return StatusCode(500, new { error = $"Failed to retrieve instances for {serviceName}" });
        }
    }

    [HttpGet("manual-call/{serviceName}")]
    public async Task<IActionResult> ManualServiceCall(string serviceName, [FromQuery] string endpoint = "")
    {
        try
        {
            // Get all instances and filter healthy ones manually
            var services = await _consulClient.Health.Service(serviceName);
            var allInstances = services.Response;
            
            // Filter for healthy instances (passing checks)
            var healthyInstances = allInstances
                .Where(instance => instance.Checks.All(check => check.Status == HealthStatus.Passing))
                .ToArray();

            if (!healthyInstances.Any())
            {
                return NotFound(new { error = $"No healthy instances found for {serviceName}" });
            }

            // Simple round-robin selection
            var instanceIndex = Interlocked.Increment(ref _requestCount) % healthyInstances.Length;
            var instance = healthyInstances[instanceIndex].Service;
            var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromSeconds(30);

            // Determine the endpoint to call
            var serviceEndpoint = string.IsNullOrEmpty(endpoint) 
                ? GetDefaultEndpoint(serviceName) 
                : endpoint;

            var serviceUrl = $"http://{instance.Address}:{instance.Port}{serviceEndpoint}";

            _logger.LogInformation("Calling {ServiceName} at {ServiceUrl}", serviceName, serviceUrl);

            var response = await httpClient.GetAsync(serviceUrl);
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                
                try
                {
                    var data = JsonSerializer.Deserialize<JsonElement>(content);
                    return Ok(new
                    {
                        serviceInstance = $"{instance.Address}:{instance.Port}",
                        serviceId = instance.ID,
                        endpoint = serviceEndpoint,
                        data = data
                    });
                }
                catch (JsonException)
                {
                    // If it's not JSON, return as string
                    return Ok(new
                    {
                        serviceInstance = $"{instance.Address}:{instance.Port}",
                        serviceId = instance.ID,
                        endpoint = serviceEndpoint,
                        data = content
                    });
                }
            }

            return StatusCode(502, new { 
                error = $"Service returned {response.StatusCode}",
                serviceUrl = serviceUrl
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Manual service call failed for {ServiceName}", serviceName);
            return StatusCode(503, new { error = $"Service call failed: {ex.Message}" });
        }
    }

    [HttpGet("load-balance/{serviceName}")]
    public async Task<IActionResult> LoadBalancedServiceCall(string serviceName)
    {
        try
        {
            var services = await _consulClient.Health.Service(serviceName);
            var allInstances = services.Response;
            
            // Filter for healthy instances
            var instances = allInstances
                .Where(instance => instance.Checks.All(check => check.Status == HealthStatus.Passing))
                .ToArray();

            if (!instances.Any())
            {
                return NotFound(new { error = $"No healthy instances available for {serviceName}" });
            }

            var results = new List<object>();
            var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromSeconds(10);

            // Call each instance to demonstrate load balancing
            foreach (var instance in instances)
            {
                try
                {
                    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                    var serviceUrl = $"http://{instance.Service.Address}:{instance.Service.Port}/health";
                    var response = await httpClient.GetAsync(serviceUrl);
                    stopwatch.Stop();
                    
                    results.Add(new
                    {
                        instanceId = instance.Service.ID,
                        address = $"{instance.Service.Address}:{instance.Service.Port}",
                        status = response.IsSuccessStatusCode ? "Healthy" : "Unhealthy",
                        responseTimeMs = stopwatch.ElapsedMilliseconds,
                        statusCode = (int)response.StatusCode,
                        consulHealthStatus = instance.Checks.FirstOrDefault()?.Status.ToString() ?? "Unknown"
                    });
                }
                catch (Exception ex)
                {
                    results.Add(new
                    {
                        instanceId = instance.Service.ID,
                        address = $"{instance.Service.Address}:{instance.Service.Port}",
                        status = "Failed",
                        error = ex.Message,
                        responseTimeMs = -1,
                        consulHealthStatus = instance.Checks.FirstOrDefault()?.Status.ToString() ?? "Unknown"
                    });
                }
            }

            return Ok(new
            {
                serviceName,
                timestamp = DateTime.UtcNow,
                loadBalancingDemo = true,
                totalInstances = instances.Length,
                instancesChecked = results.Count,
                results
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Load balancing demo failed for {ServiceName}", serviceName);
            return StatusCode(500, new { error = $"Load balancing demo failed: {ex.Message}" });
        }
    }

    [HttpGet("user-demo")]
    public async Task<IActionResult> UserServiceDemo()
    {
        try
        {
            // Get user service instances and filter healthy ones
            var services = await _consulClient.Health.Service("user-service");
            var allInstances = services.Response;
            
            var instances = allInstances
                .Where(instance => instance.Checks.All(check => check.Status == HealthStatus.Passing))
                .ToArray();

            if (!instances.Any())
            {
                return NotFound(new { error = "No healthy user-service instances available" });
            }

            // Use the first available instance
            var instance = instances[0].Service;
            var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromSeconds(30);

            // Test multiple user service endpoints
            var endpoints = new[]
            {
                "/api/users",
                "/api/users/1",
                "/health/detailed",
                "/health"
            };

            var results = new List<object>();

            foreach (var endpoint in endpoints)
            {
                try
                {
                    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                    var response = await httpClient.GetAsync($"http://{instance.Address}:{instance.Port}{endpoint}");
                    var content = await response.Content.ReadAsStringAsync();
                    stopwatch.Stop();

                    object responseData;
                    if (response.IsSuccessStatusCode)
                    {
                        try
                        {
                            responseData = JsonSerializer.Deserialize<JsonElement>(content);
                        }
                        catch (JsonException)
                        {
                            responseData = content;
                        }
                    }
                    else
                    {
                        responseData = content;
                    }
                    
                    results.Add(new
                    {
                        endpoint,
                        statusCode = (int)response.StatusCode,
                        success = response.IsSuccessStatusCode,
                        responseTimeMs = stopwatch.ElapsedMilliseconds,
                        data = responseData
                    });
                }
                catch (Exception ex)
                {
                    results.Add(new
                    {
                        endpoint,
                        error = ex.Message,
                        success = false,
                        responseTimeMs = -1
                    });
                }
            }

            return Ok(new
            {
                serviceInstance = $"{instance.Address}:{instance.Port}",
                instanceId = instance.ID,
                timestamp = DateTime.UtcNow,
                demoResults = results
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "User service demo failed");
            return StatusCode(503, new { error = $"User service demo failed: {ex.Message}" });
        }
    }

    [HttpGet("order-demo")]
    public async Task<IActionResult> OrderServiceDemo()
    {
        try
        {
            // Get order service instances and filter healthy ones
            var services = await _consulClient.Health.Service("order-service");
            var allInstances = services.Response;
            
            var instances = allInstances
                .Where(instance => instance.Checks.All(check => check.Status == HealthStatus.Passing))
                .ToArray();

            if (!instances.Any())
            {
                return NotFound(new { error = "No healthy order-service instances available" });
            }

            // Use the first available instance
            var instance = instances[0].Service;
            var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromSeconds(30);

            // Test multiple order service endpoints
            var endpoints = new[]
            {
                "/api/orders",
                "/api/orders/1",
                "/health/detailed",
                "/health"
            };

            var results = new List<object>();

            foreach (var endpoint in endpoints)
            {
                try
                {
                    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                    var response = await httpClient.GetAsync($"http://{instance.Address}:{instance.Port}{endpoint}");
                    var content = await response.Content.ReadAsStringAsync();
                    stopwatch.Stop();

                    object responseData;
                    if (response.IsSuccessStatusCode)
                    {
                        try
                        {
                            responseData = JsonSerializer.Deserialize<JsonElement>(content);
                        }
                        catch (JsonException)
                        {
                            responseData = content;
                        }
                    }
                    else
                    {
                        responseData = content;
                    }
                    
                    results.Add(new
                    {
                        endpoint,
                        statusCode = (int)response.StatusCode,
                        success = response.IsSuccessStatusCode,
                        responseTimeMs = stopwatch.ElapsedMilliseconds,
                        data = responseData
                    });
                }
                catch (Exception ex)
                {
                    results.Add(new
                    {
                        endpoint,
                        error = ex.Message,
                        success = false,
                        responseTimeMs = -1
                    });
                }
            }

            return Ok(new
            {
                serviceInstance = $"{instance.Address}:{instance.Port}",
                instanceId = instance.ID,
                timestamp = DateTime.UtcNow,
                demoResults = results
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Order service demo failed");
            return StatusCode(503, new { error = $"Order service demo failed: {ex.Message}" });
        }
    }

    [HttpGet("round-robin/{serviceName}")]
    public async Task<IActionResult> RoundRobinDemo(string serviceName)
    {
        try
        {
            var services = await _consulClient.Health.Service(serviceName);
            var allInstances = services.Response;
            
            var instances = allInstances
                .Where(instance => instance.Checks.All(check => check.Status == HealthStatus.Passing))
                .ToArray();

            if (!instances.Any())
            {
                return NotFound(new { error = $"No healthy instances available for {serviceName}" });
            }

            var results = new List<object>();
            var httpClient = _httpClientFactory.CreateClient();

            // Make multiple requests to demonstrate round-robin
            for (int i = 0; i < Math.Min(5, instances.Length); i++)
            {
                var instanceIndex = (Interlocked.Increment(ref _requestCount)) % instances.Length;
                var instance = instances[instanceIndex].Service;

                try
                {
                    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                    var response = await httpClient.GetAsync($"http://{instance.Address}:{instance.Port}/health");
                    var content = await response.Content.ReadAsStringAsync();
                    stopwatch.Stop();

                    results.Add(new
                    {
                        requestNumber = i + 1,
                        instanceId = instance.ID,
                        address = $"{instance.Address}:{instance.Port}",
                        statusCode = (int)response.StatusCode,
                        responseTimeMs = stopwatch.ElapsedMilliseconds,
                        loadBalanced = true
                    });
                }
                catch (Exception ex)
                {
                    results.Add(new
                    {
                        requestNumber = i + 1,
                        instanceId = instance.ID,
                        address = $"{instance.Address}:{instance.Port}",
                        error = ex.Message,
                        responseTimeMs = -1,
                        loadBalanced = true
                    });
                }
            }

            return Ok(new
            {
                serviceName,
                timestamp = DateTime.UtcNow,
                roundRobinDemo = true,
                totalRequests = results.Count,
                instancesUsed = instances.Length,
                results
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Round-robin demo failed for {ServiceName}", serviceName);
            return StatusCode(500, new { error = $"Round-robin demo failed: {ex.Message}" });
        }
    }

    [HttpGet("health-status/{serviceName}")]
    public async Task<IActionResult> GetServiceHealthStatus(string serviceName)
    {
        try
        {
            var services = await _consulClient.Health.Service(serviceName);
            var instances = services.Response;

            var healthStatus = instances.Select(instance => new
            {
                instance.Service.ID,
                instance.Service.Service,
                instance.Service.Address,
                instance.Service.Port,
                checks = instance.Checks.Select(check => new
                {
                    check.CheckID,
                    check.Status,
                    check.Output,
                    check.ServiceID
                }),
                overallStatus = instance.Checks.All(check => check.Status == HealthStatus.Passing) ? "Healthy" : "Unhealthy"
            });

            return Ok(new
            {
                serviceName,
                timestamp = DateTime.UtcNow,
                totalInstances = instances.Length,
                healthyInstances = instances.Count(i => i.Checks.All(c => c.Status == HealthStatus.Passing)),
                instances = healthStatus
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get health status for {ServiceName}", serviceName);
            return StatusCode(500, new { error = $"Failed to get health status: {ex.Message}" });
        }
    }

    // Helper method to get default endpoints for services
    private string GetDefaultEndpoint(string serviceName)
    {
        return serviceName.ToLower() switch
        {
            "order-service" => "/api/orders",
            "user-service" => "/api/users",
            _ => "/health"
        };
    }
    
    
    // Add this method to ServiceDiscoveryController in ApiGateway
    [HttpGet("inventory-demo")]
    public async Task<IActionResult> InventoryServiceDemo()
    {
        try
        {
            // Get healthy inventory service instances
            var services = await _consulClient.Health.Service("inventory-service");
            var allInstances = services.Response;
            
            var instances = allInstances
                .Where(instance => instance.Checks.All(check => check.Status == HealthStatus.Passing))
                .ToArray();

            if (!instances.Any())
            {
                return NotFound(new { error = "No healthy inventory-service instances available" });
            }

            // Use the first available instance
            var instance = instances[0].Service;
            var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromSeconds(30);

            // Test multiple inventory service endpoints
            var endpoints = new[]
            {
                "/api/inventory",
                "/api/inventory/1",
                "/api/inventory/low-stock",
                "/api/inventory/stats",
                "/health/detailed"
            };

            var results = new List<object>();

            foreach (var endpoint in endpoints)
            {
                try
                {
                    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                    var response = await httpClient.GetAsync($"http://{instance.Address}:{instance.Port}{endpoint}");
                    var content = await response.Content.ReadAsStringAsync();
                    stopwatch.Stop();

                    object responseData;
                    if (response.IsSuccessStatusCode)
                    {
                        try
                        {
                            responseData = JsonSerializer.Deserialize<JsonElement>(content);
                        }
                        catch (JsonException)
                        {
                            responseData = content;
                        }
                    }
                    else
                    {
                        responseData = content;
                    }
                    
                    results.Add(new
                    {
                        endpoint,
                        statusCode = (int)response.StatusCode,
                        success = response.IsSuccessStatusCode,
                        responseTimeMs = stopwatch.ElapsedMilliseconds,
                        data = responseData
                    });
                }
                catch (Exception ex)
                {
                    results.Add(new
                    {
                        endpoint,
                        error = ex.Message,
                        success = false,
                        responseTimeMs = -1
                    });
                }
            }

            return Ok(new
            {
                serviceInstance = $"{instance.Address}:{instance.Port}",
                instanceId = instance.ID,
                timestamp = DateTime.UtcNow,
                demoResults = results
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Inventory service demo failed");
            return StatusCode(503, new { error = $"Inventory service demo failed: {ex.Message}" });
        }
    }
}