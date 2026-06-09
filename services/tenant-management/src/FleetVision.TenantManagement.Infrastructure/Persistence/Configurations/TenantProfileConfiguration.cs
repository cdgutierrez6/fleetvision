using FleetVision.TenantManagement.Domain.Entities;
using FleetVision.TenantManagement.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FleetVision.TenantManagement.Infrastructure.Persistence.Configurations;

public sealed class TenantProfileConfiguration : IEntityTypeConfiguration<TenantProfile>
{
    public void Configure(EntityTypeBuilder<TenantProfile> builder)
    {
        builder.ToTable("tenant_profiles");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasColumnName("id");

        builder.Property(t => t.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(t => t.CompanyName)
            .HasColumnName("company_name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(t => t.Slug)
            .HasColumnName("slug")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(t => t.Plan)
            .HasColumnName("plan")
            .HasConversion<string>()
            .IsRequired();

        builder.Property(t => t.MaxVehicles)
            .HasColumnName("max_vehicles")
            .IsRequired();

        builder.Property(t => t.MaxUsers)
            .HasColumnName("max_users")
            .IsRequired();

        builder.Property(t => t.BillingEmail)
            .HasColumnName("billing_email")
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(t => t.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        builder.Property(t => t.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(t => t.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        // One profile per tenant — enforced at DB level
        builder.HasIndex(t => t.TenantId)
            .IsUnique()
            .HasDatabaseName("idx_tenant_profiles_tenant_id");

        builder.HasIndex(t => t.IsActive)
            .HasDatabaseName("idx_tenant_profiles_is_active");
    }
}
