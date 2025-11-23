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
    public async Task<IActionResult> ManualServiceCall(string serviceName)
    {
        try
        {
            // Get healthy instances only
            var services = await _consulClient.Health.Service(serviceName, default);
            var healthyInstances = services.Response;

            if (!healthyInstances.Any())
            {
                return NotFound(new { error = $"No healthy instances found for {serviceName}" });
            }

            // Simple round-robin selection
            var instance = healthyInstances[DateTime.UtcNow.Second % healthyInstances.Length].Service;
            var httpClient = _httpClientFactory.CreateClient();

            var serviceUrl = $"http://{instance.Address}:{instance.Port}/api/orders";

            _logger.LogInformation("Calling {ServiceName} at {ServiceUrl}", serviceName, serviceUrl);

            var response = await httpClient.GetAsync(serviceUrl);
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var orders = JsonSerializer.Deserialize<List<object>>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return Ok(new
                {
                    serviceInstance = $"{instance.Address}:{instance.Port}",
                    serviceId = instance.ID,
                    data = orders
                });
            }

            return StatusCode(502, new { error = $"Service returned {response.StatusCode}" });
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
            var services = await _consulClient.Health.Service(serviceName, default);
            var instances = services.Response;

            if (!instances.Any())
            {
                return NotFound(new { error = $"No instances available for {serviceName}" });
            }

            var results = new List<object>();
            var httpClient = _httpClientFactory.CreateClient();

            // Call each instance to demonstrate load balancing
            foreach (var instance in instances.Take(3)) // Limit to 3 for demo
            {
                try
                {
                    var serviceUrl = $"http://{instance.Service.Address}:{instance.Service.Port}/health";
                    var response = await httpClient.GetAsync(serviceUrl);
                    
                    results.Add(new
                    {
                        instanceId = instance.Service.ID,
                        address = $"{instance.Service.Address}:{instance.Service.Port}",
                        status = response.IsSuccessStatusCode ? "Healthy" : "Unhealthy",
                        responseTime = "N/A" // In real scenario, measure this
                    });
                }
                catch (Exception ex)
                {
                    results.Add(new
                    {
                        instanceId = instance.Service.ID,
                        address = $"{instance.Service.Address}:{instance.Service.Port}",
                        status = "Failed",
                        error = ex.Message
                    });
                }
            }

            return Ok(new
            {
                serviceName,
                loadBalancingDemo = true,
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
}