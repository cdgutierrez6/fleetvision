using FleetVision.FleetAssets.Domain.Entities;
using FleetVision.FleetAssets.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FleetVision.FleetAssets.Infrastructure.Persistence.Configurations;

public sealed class VehicleConfiguration : IEntityTypeConfiguration<Vehicle>
{
    public void Configure(EntityTypeBuilder<Vehicle> builder)
    {
        builder.ToTable("vehicles");
        builder.HasKey(v => v.Id);

        builder.Property(v => v.Id).HasColumnName("id");
        builder.Property(v => v.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(v => v.FleetId).HasColumnName("fleet_id").IsRequired();
        builder.Property(v => v.Plate).HasColumnName("plate").HasMaxLength(20).IsRequired();
        builder.Property(v => v.Vin).HasColumnName("vin").HasMaxLength(17);
        builder.Property(v => v.Brand).HasColumnName("brand").HasMaxLength(50).IsRequired();
        builder.Property(v => v.Model).HasColumnName("model").HasMaxLength(50).IsRequired();
        builder.Property(v => v.Year).HasColumnName("year").IsRequired();
        builder.Property(v => v.OdometerKm).HasColumnName("odometer_km").IsRequired();
        builder.Property(v => v.Status)
               .HasColumnName("status")
               .HasConversion<string>()
               .HasMaxLength(20)
               .IsRequired();
        builder.Property(v => v.LastKnownPosition)
               .HasColumnName("last_known_position")
               .HasColumnType("geometry(Point, 4326)");
        builder.Property(v => v.IsDeleted).HasColumnName("is_deleted").IsRequired();
        builder.Property(v => v.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(v => v.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasIndex(v => v.TenantId).HasDatabaseName("idx_vehicles_tenant");
        builder.HasIndex(v => v.FleetId).HasDatabaseName("idx_vehicles_fleet");

        builder.HasOne<Fleet>()
               .WithMany()
               .HasForeignKey(v => v.FleetId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
