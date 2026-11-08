using Microsoft.EntityFrameworkCore;
using Studium_Users.Domain.Entities.AuditLogs;
using Studium_Users.Domain.Entities.Users;

namespace Studium_Users.Data;

public class UsersDbContext(DbContextOptions<UsersDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(UsersDbContext).Assembly);
    }
}
