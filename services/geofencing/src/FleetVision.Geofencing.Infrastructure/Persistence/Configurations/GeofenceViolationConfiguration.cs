using FleetVision.Geofencing.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FleetVision.Geofencing.Infrastructure.Persistence.Configurations;

public sealed class GeofenceViolationConfiguration : IEntityTypeConfiguration<GeofenceViolation>
{
    public void Configure(EntityTypeBuilder<GeofenceViolation> builder)
    {
        builder.ToTable("geofence_violations");

        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).HasColumnName("id");
        builder.Property(v => v.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(v => v.GeofenceId).HasColumnName("geofence_id").IsRequired();
        builder.Property(v => v.VehicleId).HasColumnName("vehicle_id").IsRequired();
        builder.Property(v => v.DriverId).HasColumnName("driver_id");

        builder.Property(v => v.ViolationType)
               .HasColumnName("violation_type")
               .HasConversion<string>()
               .HasMaxLength(30)
               .IsRequired();

        builder.Property(v => v.Position)
               .HasColumnName("position")
               .HasColumnType("geometry(Point, 4326)")
               .IsRequired();

        builder.Property(v => v.ActualSpeedKmh).HasColumnName("actual_speed_kmh");
        builder.Property(v => v.LimitSpeedKmh).HasColumnName("limit_speed_kmh");
        builder.Property(v => v.OccurredAt).HasColumnName("occurred_at").IsRequired();

        builder.HasIndex(v => v.TenantId).HasDatabaseName("idx_violations_tenant_id");
        builder.HasIndex(v => new { v.GeofenceId, v.OccurredAt })
               .HasDatabaseName("idx_violations_geofence_occurred");
        builder.HasIndex(v => new { v.VehicleId, v.OccurredAt })
               .HasDatabaseName("idx_violations_vehicle_occurred");
    }
}
