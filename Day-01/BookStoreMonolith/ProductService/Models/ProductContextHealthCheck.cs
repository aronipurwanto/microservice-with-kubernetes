using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using ProductService.Models;

public class ProductContextHealthCheck : IHealthCheck
{
    private readonly ProductContext _context;

    public ProductContextHealthCheck(ProductContext context)
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