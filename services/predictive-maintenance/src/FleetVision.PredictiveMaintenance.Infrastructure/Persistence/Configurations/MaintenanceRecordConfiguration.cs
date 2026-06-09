using FleetVision.PredictiveMaintenance.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FleetVision.PredictiveMaintenance.Infrastructure.Persistence.Configurations;

public sealed class MaintenanceRecordConfiguration : IEntityTypeConfiguration<MaintenanceRecord>
{
    public void Configure(EntityTypeBuilder<MaintenanceRecord> b)
    {
        b.ToTable("maintenance_records");
        b.HasKey(e => e.Id);

        b.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(e => e.TenantId).HasColumnName("tenant_id").IsRequired();
        b.Property(e => e.VehicleId).HasColumnName("vehicle_id").IsRequired();
        b.Property(e => e.RecordType).HasColumnName("record_type").HasMaxLength(50).IsRequired();
        b.Property(e => e.TriggeredBy).HasColumnName("triggered_by").HasMaxLength(100).IsRequired();
        b.Property(e => e.Obd2Code).HasColumnName("obd2_code").HasMaxLength(20);
        b.Property(e => e.OdometerKm).HasColumnName("odometer_km").HasPrecision(10, 2);
        b.Property(e => e.ThresholdKm).HasColumnName("threshold_km").HasPrecision(10, 2);
        b.Property(e => e.Notes).HasColumnName("notes");
        b.Property(e => e.ResolvedAt).HasColumnName("resolved_at");
        b.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();

        b.HasIndex(e => new { e.TenantId, e.VehicleId }).HasDatabaseName("idx_maintenance_tenant_vehicle");
        b.HasIndex(e => e.CreatedAt).HasDatabaseName("idx_maintenance_created").IsDescending();
    }
}
