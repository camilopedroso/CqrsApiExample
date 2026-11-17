using CqrsApiExample.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CqrsApiExample.Infrastructure.Persistence.Configurations;

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages", Schemas.Messaging);

        // Guid v7 key: non-clustered, rows clustered by time (see UserConfiguration).
        builder.HasKey(message => message.Id).IsClustered(false);
        builder.Property(message => message.Id).ValueGeneratedNever();
        builder.HasIndex(message => message.OccurredAt).IsClustered();

        builder.Property(message => message.Type).HasMaxLength(256).IsRequired();
        builder.Property(message => message.Content).IsRequired();

        // Keeps the polling query cheap: only messages still pending are indexed.
        builder.HasIndex(message => message.NextAttemptAt)
               .HasFilter("[ProcessedAt] IS NULL")
               .HasDatabaseName("IX_OutboxMessages_Pending");
    }
}

internal sealed class OutboxMessageConsumerConfiguration : IEntityTypeConfiguration<OutboxMessageConsumer>
{
    public void Configure(EntityTypeBuilder<OutboxMessageConsumer> builder)
    {
        builder.ToTable("OutboxMessageConsumers", Schemas.Messaging);

        builder.HasKey(consumer => new { consumer.MessageId, consumer.HandlerName });
        builder.Property(consumer => consumer.HandlerName).HasMaxLength(256);

        builder.HasOne<OutboxMessage>()
               .WithMany()
               .HasForeignKey(consumer => consumer.MessageId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
