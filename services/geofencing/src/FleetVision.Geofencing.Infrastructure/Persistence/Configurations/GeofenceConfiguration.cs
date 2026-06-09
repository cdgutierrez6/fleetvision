using FleetVision.Geofencing.Domain.Entities;
using FleetVision.Geofencing.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FleetVision.Geofencing.Infrastructure.Persistence.Configurations;

public sealed class GeofenceConfiguration : IEntityTypeConfiguration<Geofence>
{
    public void Configure(EntityTypeBuilder<Geofence> builder)
    {
        builder.ToTable("geofences");

        builder.HasKey(g => g.Id);
        builder.Property(g => g.Id).HasColumnName("id");
        builder.Property(g => g.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(g => g.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        builder.Property(g => g.Description).HasColumnName("description").HasMaxLength(500);

        builder.Property(g => g.Boundary)
               .HasColumnName("boundary")
               .HasColumnType("geometry(Polygon, 4326)")
               .IsRequired();

        builder.Property(g => g.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(g => g.MaxSpeedKmh).HasColumnName("max_speed_kmh");
        builder.Property(g => g.AllowedFrom).HasColumnName("allowed_from");
        builder.Property(g => g.AllowedTo).HasColumnName("allowed_to");

        builder.Property(g => g.Direction)
               .HasColumnName("direction")
               .HasConversion<string>()
               .HasMaxLength(20)
               .IsRequired();

        builder.Property(g => g.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(g => g.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasIndex(g => g.TenantId).HasDatabaseName("idx_geofences_tenant_id");
        builder.HasIndex(g => new { g.TenantId, g.Name }).IsUnique()
               .HasDatabaseName("idx_geofences_tenant_name_unique");
        builder.HasIndex(g => g.Boundary).HasMethod("GIST")
               .HasDatabaseName("idx_geofences_boundary_gist");
    }
}
