using FleetVision.Billing.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FleetVision.Billing.Infrastructure.Persistence.Configurations;

public sealed class PlanChangeAuditEntryConfiguration : IEntityTypeConfiguration<PlanChangeAuditEntry>
{
    public void Configure(EntityTypeBuilder<PlanChangeAuditEntry> builder)
    {
        builder.ToTable("plan_change_audit");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.OldPlan).HasColumnName("old_plan").HasMaxLength(50).IsRequired();
        builder.Property(x => x.NewPlan).HasColumnName("new_plan").HasMaxLength(50).IsRequired();
        builder.Property(x => x.Source).HasColumnName("source").HasMaxLength(100).IsRequired();
        builder.Property(x => x.StripeEventId).HasColumnName("stripe_event_id").HasMaxLength(100);
        builder.Property(x => x.OccurredAt).HasColumnName("occurred_at").IsRequired();

        builder.HasIndex(x => x.TenantId).HasDatabaseName("ix_plan_change_audit_tenant_id");
        builder.HasIndex(x => x.OccurredAt).HasDatabaseName("ix_plan_change_audit_occurred_at");
    }
}
