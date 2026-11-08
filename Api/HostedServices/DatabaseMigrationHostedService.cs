using Microsoft.EntityFrameworkCore;
using Studium_Users.Data;

namespace Studium_Users.Api.HostedServices;

/// <summary>
/// Applies pending EF Core migrations when the host starts. Runs only on host startup,
/// so CLI commands such as Wolverine's <c>codegen write</c> never touch the database.
/// </summary>
public class DatabaseMigrationHostedService(IServiceScopeFactory scopeFactory) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
        await db.Database.MigrateAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
