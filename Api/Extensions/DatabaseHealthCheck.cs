using Microsoft.Extensions.Diagnostics.HealthChecks;
using Studium_Users.Data;

namespace Studium_Users.Api.Extensions;

/// <summary>
/// Verifies PostgreSQL database connectivity for Kubernetes readiness probes.
/// </summary>
public class DatabaseHealthCheck(UsersDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var canConnect = await db.Database.CanConnectAsync(cancellationToken);
            return canConnect
                ? HealthCheckResult.Healthy("PostgreSQL database reachable.")
                : HealthCheckResult.Unhealthy("PostgreSQL database connection refused.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("PostgreSQL database health check failed.", ex);
        }
    }
}
