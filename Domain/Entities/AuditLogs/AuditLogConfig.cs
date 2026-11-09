using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Studium_Users.Domain.Entities.AuditLogs;

public class AuditLogConfig : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> entity)
    {
        entity.ToTable("AuditLogs");
        entity.HasKey(a => a.Id);

        entity.Property(a => a.Id).ValueGeneratedNever();
        entity.Property(a => a.UserId).HasMaxLength(128).IsRequired();
        entity.Property(a => a.Action).IsRequired();
        entity.Property(a => a.TimestampUtc).HasDefaultValueSql("now()").IsRequired();

        entity.HasIndex(a => a.UserId);
        entity.HasIndex(a => a.TimestampUtc);
        entity.HasIndex(a => a.Action);
    }
}
