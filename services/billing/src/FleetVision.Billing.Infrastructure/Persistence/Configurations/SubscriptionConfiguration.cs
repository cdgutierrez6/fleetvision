using FleetVision.Billing.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FleetVision.Billing.Infrastructure.Persistence.Configurations;

public sealed class SubscriptionConfiguration : IEntityTypeConfiguration<Subscription>
{
    public void Configure(EntityTypeBuilder<Subscription> builder)
    {
        builder.ToTable("subscriptions");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id");

        builder.Property(s => s.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(s => s.StripeCustomerId)
            .HasColumnName("stripe_customer_id")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(s => s.StripeSubscriptionId)
            .HasColumnName("stripe_subscription_id")
            .HasMaxLength(255);

        builder.Property(s => s.Plan)
            .HasColumnName("plan")
            .HasConversion<string>()
            .IsRequired();

        builder.Property(s => s.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .IsRequired();

        builder.Property(s => s.CurrentPeriodStart)
            .HasColumnName("current_period_start");

        builder.Property(s => s.CurrentPeriodEnd)
            .HasColumnName("current_period_end");

        builder.Property(s => s.CancelAtPeriodEnd)
            .HasColumnName("cancel_at_period_end")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(s => s.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(s => s.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.HasIndex(s => s.TenantId)
            .IsUnique()
            .HasDatabaseName("idx_subscriptions_tenant_id");

        builder.HasIndex(s => s.StripeSubscriptionId)
            .HasDatabaseName("idx_subscriptions_stripe_subscription_id");
    }
}
