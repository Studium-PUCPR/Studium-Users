using Microsoft.EntityFrameworkCore;
using Studium_Users.Domain.Entities.Inbox;
using Studium_Users.Domain.Entities.Users;

namespace Studium_Users.Data;

public class UsersDbContext(DbContextOptions<UsersDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(User).Assembly);
    }
}
