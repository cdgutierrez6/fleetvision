using FleetVision.Billing.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FleetVision.Billing.Infrastructure.Persistence.Configurations;

public sealed class BillingOutboxEventConfiguration : IEntityTypeConfiguration<BillingOutboxEvent>
{
    public void Configure(EntityTypeBuilder<BillingOutboxEvent> builder)
    {
        builder.ToTable("billing_outbox_events");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");

        builder.Property(e => e.Topic)
            .HasColumnName("topic")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.PartitionKey)
            .HasColumnName("partition_key")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.Payload)
            .HasColumnName("payload")
            .IsRequired();

        builder.Property(e => e.PublishedAt)
            .HasColumnName("published_at");

        builder.Property(e => e.RetryCount)
            .HasColumnName("retry_count")
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(e => e.LastError)
            .HasColumnName("last_error");

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        // Partial index for fast unprocessed event lookup
        builder.HasIndex(e => e.CreatedAt)
            .HasFilter("published_at IS NULL")
            .HasDatabaseName("idx_billing_outbox_unprocessed");
    }
}
