using FleetVision.FleetAssets.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FleetVision.FleetAssets.Infrastructure.Persistence.Configurations;

public sealed class VehicleAssignmentConfiguration : IEntityTypeConfiguration<VehicleAssignment>
{
    public void Configure(EntityTypeBuilder<VehicleAssignment> builder)
    {
        builder.ToTable("vehicle_assignments");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id).HasColumnName("id");
        builder.Property(a => a.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(a => a.VehicleId).HasColumnName("vehicle_id").IsRequired();
        builder.Property(a => a.DriverId).HasColumnName("driver_id").IsRequired();
        builder.Property(a => a.StartedAt).HasColumnName("started_at").IsRequired();
        builder.Property(a => a.EndedAt).HasColumnName("ended_at");

        builder.HasIndex(a => a.TenantId).HasDatabaseName("idx_assignments_tenant");
        builder.HasIndex(a => a.VehicleId).HasDatabaseName("idx_assignments_vehicle");
        builder.HasIndex(a => a.DriverId).HasDatabaseName("idx_assignments_driver");

        builder.HasOne<Vehicle>()
               .WithMany()
               .HasForeignKey(a => a.VehicleId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Driver>()
               .WithMany()
               .HasForeignKey(a => a.DriverId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
