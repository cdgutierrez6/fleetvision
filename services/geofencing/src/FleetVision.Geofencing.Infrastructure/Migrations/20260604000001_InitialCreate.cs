using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FleetVision.Geofencing.Infrastructure.Migrations;

public partial class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE IF NOT EXISTS geofences (
                id              UUID        NOT NULL DEFAULT uuid_generate_v4() PRIMARY KEY,
                tenant_id       UUID        NOT NULL,
                name            VARCHAR(100) NOT NULL,
                description     VARCHAR(500),
                boundary        geometry(Polygon, 4326) NOT NULL,
                is_active       BOOLEAN     NOT NULL DEFAULT TRUE,
                max_speed_kmh   INT,
                allowed_from    TIME,
                allowed_to      TIME,
                direction       VARCHAR(20)  NOT NULL DEFAULT 'Both',
                created_at      TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
                updated_at      TIMESTAMPTZ  NOT NULL DEFAULT NOW()
            );

            CREATE INDEX IF NOT EXISTS idx_geofences_tenant_id
                ON geofences(tenant_id);

            CREATE UNIQUE INDEX IF NOT EXISTS idx_geofences_tenant_name_unique
                ON geofences(tenant_id, name);

            CREATE INDEX IF NOT EXISTS idx_geofences_boundary_gist
                ON geofences USING GIST(boundary);
            """);

        migrationBuilder.Sql("""
            -- No FK to geofences.id intentional: violations are forensic records.
            -- A deleted geofence must not cascade-delete its historical violations.
            CREATE TABLE IF NOT EXISTS geofence_violations (
                id              UUID        NOT NULL DEFAULT uuid_generate_v4() PRIMARY KEY,
                tenant_id       UUID        NOT NULL,
                geofence_id     UUID        NOT NULL,
                vehicle_id      UUID        NOT NULL,
                driver_id       UUID,
                violation_type  VARCHAR(30)  NOT NULL,
                position        geometry(Point, 4326) NOT NULL,
                actual_speed_kmh DOUBLE PRECISION,
                limit_speed_kmh  INT,
                occurred_at     TIMESTAMPTZ  NOT NULL DEFAULT NOW()
            );

            CREATE INDEX IF NOT EXISTS idx_violations_tenant_id
                ON geofence_violations(tenant_id);

            CREATE INDEX IF NOT EXISTS idx_violations_geofence_occurred
                ON geofence_violations(geofence_id, occurred_at);

            CREATE INDEX IF NOT EXISTS idx_violations_vehicle_occurred
                ON geofence_violations(vehicle_id, occurred_at);
            """);

        migrationBuilder.Sql("""
            CREATE TABLE IF NOT EXISTS vehicle_geofence_states (
                id                  UUID        NOT NULL DEFAULT uuid_generate_v4() PRIMARY KEY,
                tenant_id           UUID        NOT NULL,
                vehicle_id          UUID        NOT NULL,
                geofence_id         UUID        NOT NULL,
                is_inside           BOOLEAN     NOT NULL DEFAULT FALSE,
                last_evaluated_at   TIMESTAMPTZ  NOT NULL DEFAULT NOW()
            );

            CREATE UNIQUE INDEX IF NOT EXISTS idx_vehicle_geofence_state_unique
                ON vehicle_geofence_states(vehicle_id, geofence_id);

            CREATE INDEX IF NOT EXISTS idx_vehicle_geofence_state_tenant
                ON vehicle_geofence_states(tenant_id);
            """);

        migrationBuilder.Sql("""
            CREATE TABLE IF NOT EXISTS geofencing_outbox_events (
                id              UUID        NOT NULL DEFAULT uuid_generate_v4() PRIMARY KEY,
                topic           VARCHAR(200) NOT NULL,
                partition_key   VARCHAR(36)  NOT NULL,
                payload         BYTEA        NOT NULL,
                created_at      TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
                published_at    TIMESTAMPTZ,
                retry_count     INT          NOT NULL DEFAULT 0,
                last_error      TEXT
            );

            CREATE INDEX IF NOT EXISTS ix_geofencing_outbox_unpublished
                ON geofencing_outbox_events(created_at)
                WHERE published_at IS NULL;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TABLE IF EXISTS geofencing_outbox_events;");
        migrationBuilder.Sql("DROP TABLE IF EXISTS vehicle_geofence_states;");
        migrationBuilder.Sql("DROP TABLE IF EXISTS geofence_violations;");
        migrationBuilder.Sql("DROP TABLE IF EXISTS geofences;");
    }
}
