using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Studium_Users.Domain.Entities.Users;

public class UserConfig : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> entity)
    {
        entity.ToTable("Users");
        entity.HasKey(u => u.Id);

        entity.Property(u => u.Id).HasMaxLength(128).IsRequired();
        entity.Property(u => u.FullName).HasMaxLength(150).IsRequired();
        entity.Property(u => u.Email).HasMaxLength(150).IsRequired();
        entity.Property(u => u.RegistrationNumber).HasMaxLength(50).IsRequired();
        entity.Property(u => u.Role).HasDefaultValue(UserRole.Student).IsRequired();
        entity.Property(u => u.PhoneNumber).HasMaxLength(30);
        entity.Property(u => u.BlockReason).HasMaxLength(500);

        entity.Property(u => u.NotifyReminders).HasDefaultValue(true).IsRequired();
        entity.Property(u => u.NotifyConfirmations).HasDefaultValue(true).IsRequired();
        entity.Property(u => u.IsBlocked).HasDefaultValue(false).IsRequired();
        entity.Property(u => u.NoShowCount30Days).HasDefaultValue(0).IsRequired();
        entity.Property(u => u.TotalReservations30Days).HasDefaultValue(0).IsRequired();
        entity.Property(u => u.CompletedReservations30Days).HasDefaultValue(0).IsRequired();
        entity.Property(u => u.CancelledReservations30Days).HasDefaultValue(0).IsRequired();

        entity.Property(u => u.CreatedAtUtc).HasDefaultValueSql("now()").IsRequired();

        entity.HasMany(u => u.AuditLogs)
              .WithOne()
              .HasForeignKey(a => a.UserId)
              .OnDelete(DeleteBehavior.Restrict);

        entity.HasIndex(u => u.Email).IsUnique();
        entity.HasIndex(u => u.RegistrationNumber).IsUnique();
        entity.HasIndex(u => u.Role);
        entity.HasIndex(u => u.IsBlocked);
    }
}
