using Microsoft.Extensions.Diagnostics.HealthChecks;
using UserService.Models;

namespace UserService.Controllers;

public class UserContextHealthCheck : IHealthCheck
{
    private readonly UserContext _context; 
    public UserContextHealthCheck(UserContext context)
    {
        _context = context;
    }
    
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await _context.Database.CanConnectAsync(cancellationToken);
            return HealthCheckResult.Healthy("ProductContext is healthy.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("ProductContext is unhealthy.", ex);
        }
    }
}