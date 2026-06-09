using FleetVision.Geofencing.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FleetVision.Geofencing.Infrastructure.Persistence.Configurations;

public sealed class GeofencingOutboxEventConfiguration : IEntityTypeConfiguration<GeofencingOutboxEvent>
{
    public void Configure(EntityTypeBuilder<GeofencingOutboxEvent> builder)
    {
        builder.ToTable("geofencing_outbox_events");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.Topic).HasColumnName("topic").HasMaxLength(200).IsRequired();
        builder.Property(e => e.PartitionKey).HasColumnName("partition_key").HasMaxLength(36).IsRequired();
        builder.Property(e => e.Payload).HasColumnName("payload").IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.PublishedAt).HasColumnName("published_at");
        builder.Property(e => e.RetryCount).HasColumnName("retry_count").HasDefaultValue(0);
        builder.Property(e => e.LastError).HasColumnName("last_error");

        // Partial index: only unpublished events — relay worker reads these exclusively
        builder.HasIndex(e => e.CreatedAt)
               .HasDatabaseName("ix_geofencing_outbox_unpublished")
               .HasFilter("published_at IS NULL");
    }
}
