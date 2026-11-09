using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Studium_Users.Domain.Entities.Inbox;

public class InboxMessageConfig : IEntityTypeConfiguration<InboxMessage>
{
    public void Configure(EntityTypeBuilder<InboxMessage> entity)
    {
        entity.ToTable("InboxMessages");
        entity.HasKey(m => new { m.Id, m.HandlerName });

        entity.Property(m => m.HandlerName).HasMaxLength(150).IsRequired();
        entity.Property(m => m.ReceivedAtUtc).IsRequired();

        entity.HasIndex(m => m.ReceivedAtUtc);
    }
}
