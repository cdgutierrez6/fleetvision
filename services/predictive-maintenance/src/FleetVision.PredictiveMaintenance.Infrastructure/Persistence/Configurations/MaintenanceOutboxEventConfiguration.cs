using FleetVision.PredictiveMaintenance.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FleetVision.PredictiveMaintenance.Infrastructure.Persistence.Configurations;

public sealed class MaintenanceOutboxEventConfiguration : IEntityTypeConfiguration<MaintenanceOutboxEvent>
{
    public void Configure(EntityTypeBuilder<MaintenanceOutboxEvent> b)
    {
        b.ToTable("maintenance_outbox_events");
        b.HasKey(e => e.Id);

        b.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(e => e.AggregateId).HasColumnName("aggregate_id").IsRequired();
        b.Property(e => e.Topic).HasColumnName("topic").HasMaxLength(200).IsRequired();
        b.Property(e => e.PartitionKey).HasColumnName("partition_key").HasMaxLength(200).IsRequired();
        b.Property(e => e.Payload).HasColumnName("payload").IsRequired();
        b.Property(e => e.RetryCount).HasColumnName("retry_count");
        b.Property(e => e.LastError).HasColumnName("last_error");
        b.Property(e => e.PublishedAt).HasColumnName("published_at");
        b.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();

        b.HasIndex(e => e.PublishedAt).HasDatabaseName("idx_maintenance_outbox_unpublished")
            .HasFilter("published_at IS NULL");
    }
}
