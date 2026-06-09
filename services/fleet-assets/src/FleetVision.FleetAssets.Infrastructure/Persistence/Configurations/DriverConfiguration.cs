using FleetVision.FleetAssets.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FleetVision.FleetAssets.Infrastructure.Persistence.Configurations;

public sealed class DriverConfiguration : IEntityTypeConfiguration<Driver>
{
    public void Configure(EntityTypeBuilder<Driver> builder)
    {
        builder.ToTable("drivers");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.Id).HasColumnName("id");
        builder.Property(d => d.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(d => d.FullName).HasColumnName("full_name").HasMaxLength(100).IsRequired();
        builder.Property(d => d.LicenseNumber).HasColumnName("license_number").HasMaxLength(30).IsRequired();
        builder.Property(d => d.Phone).HasColumnName("phone").HasMaxLength(20);
        builder.Property(d => d.Email).HasColumnName("email").HasMaxLength(150);
        builder.Property(d => d.Status)
               .HasColumnName("status")
               .HasConversion<string>()
               .HasMaxLength(20)
               .IsRequired();
        builder.Property(d => d.IsDeleted).HasColumnName("is_deleted").IsRequired();
        builder.Property(d => d.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(d => d.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasIndex(d => d.TenantId).HasDatabaseName("idx_drivers_tenant");
    }
}
