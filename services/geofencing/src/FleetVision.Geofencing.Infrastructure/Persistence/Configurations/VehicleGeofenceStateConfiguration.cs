using FleetVision.Geofencing.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FleetVision.Geofencing.Infrastructure.Persistence.Configurations;

public sealed class VehicleGeofenceStateConfiguration : IEntityTypeConfiguration<VehicleGeofenceState>
{
    public void Configure(EntityTypeBuilder<VehicleGeofenceState> builder)
    {
        builder.ToTable("vehicle_geofence_states");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id");
        builder.Property(s => s.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(s => s.VehicleId).HasColumnName("vehicle_id").IsRequired();
        builder.Property(s => s.GeofenceId).HasColumnName("geofence_id").IsRequired();
        builder.Property(s => s.IsInside).HasColumnName("is_inside").IsRequired();
        builder.Property(s => s.LastEvaluatedAt).HasColumnName("last_evaluated_at").IsRequired();

        builder.HasIndex(s => new { s.VehicleId, s.GeofenceId }).IsUnique()
               .HasDatabaseName("idx_vehicle_geofence_state_unique");
        builder.HasIndex(s => s.TenantId).HasDatabaseName("idx_vehicle_geofence_state_tenant");
    }
}
