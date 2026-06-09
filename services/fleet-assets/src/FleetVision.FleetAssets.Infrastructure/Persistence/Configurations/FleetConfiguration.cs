using FleetVision.FleetAssets.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FleetVision.FleetAssets.Infrastructure.Persistence.Configurations;

public sealed class FleetConfiguration : IEntityTypeConfiguration<Fleet>
{
    public void Configure(EntityTypeBuilder<Fleet> builder)
    {
        builder.ToTable("fleets");
        builder.HasKey(f => f.Id);

        builder.Property(f => f.Id).HasColumnName("id");
        builder.Property(f => f.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(f => f.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        builder.Property(f => f.Description).HasColumnName("description").HasMaxLength(500);
        builder.Property(f => f.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(f => f.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasIndex(f => f.TenantId).HasDatabaseName("idx_fleets_tenant");
    }
}
