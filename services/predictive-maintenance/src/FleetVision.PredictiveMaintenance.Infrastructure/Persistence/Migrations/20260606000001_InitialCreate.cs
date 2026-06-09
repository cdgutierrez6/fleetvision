using FleetVision.PredictiveMaintenance.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FleetVision.PredictiveMaintenance.Infrastructure.Migrations;

[DbContext(typeof(MaintenanceDbContext))]
[Migration("20260606000001_InitialCreate")]
public partial class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE IF NOT EXISTS maintenance_records (
                id              UUID        NOT NULL DEFAULT gen_random_uuid() PRIMARY KEY,
                tenant_id       UUID        NOT NULL,
                vehicle_id      UUID        NOT NULL,
                record_type     VARCHAR(50)  NOT NULL,
                triggered_by    VARCHAR(100) NOT NULL,
                obd2_code       VARCHAR(20),
                odometer_km     DECIMAL(10,2),
                threshold_km    DECIMAL(10,2),
                notes           TEXT,
                resolved_at     TIMESTAMPTZ,
                created_at      TIMESTAMPTZ  NOT NULL DEFAULT NOW()
            );

            ALTER TABLE maintenance_records ENABLE ROW LEVEL SECURITY;

            CREATE POLICY tenant_isolation ON maintenance_records
                USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::UUID);

            CREATE INDEX IF NOT EXISTS idx_maintenance_tenant_vehicle
                ON maintenance_records(tenant_id, vehicle_id);

            CREATE INDEX IF NOT EXISTS idx_maintenance_created
                ON maintenance_records(created_at DESC);
            """);

        migrationBuilder.Sql("""
            CREATE TABLE IF NOT EXISTS maintenance_outbox_events (
                id              UUID        NOT NULL DEFAULT gen_random_uuid() PRIMARY KEY,
                aggregate_id    UUID        NOT NULL,
                topic           VARCHAR(200) NOT NULL,
                partition_key   VARCHAR(200) NOT NULL,
                payload         BYTEA        NOT NULL,
                retry_count     INT          NOT NULL DEFAULT 0,
                last_error      TEXT,
                published_at    TIMESTAMPTZ,
                created_at      TIMESTAMPTZ  NOT NULL DEFAULT NOW()
            );

            CREATE INDEX IF NOT EXISTS idx_maintenance_outbox_unpublished
                ON maintenance_outbox_events(created_at ASC)
                WHERE published_at IS NULL;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TABLE IF EXISTS maintenance_outbox_events;");
        migrationBuilder.Sql("DROP TABLE IF EXISTS maintenance_records;");
    }
}
