-- RFC-011-A: TimescaleDB Continuous Aggregates for Reporting Service
-- Run against telemetry_db
-- Idempotent: safe to re-run

-- ─── Continuous Aggregate: daily KPIs per vehicle per tenant ──────────────────
-- Incremental refresh (only new chunks) — avoids full hypertable rescan.
-- distance_km = MAX(odometer_km) - MIN(odometer_km) per vehicle per day
-- (delta-based, tolerates gaps in telemetry delivery)

CREATE MATERIALIZED VIEW IF NOT EXISTS daily_fleet_kpis
WITH (timescaledb.continuous) AS
SELECT
    tenant_id,
    vehicle_id,
    time_bucket('1 day', time)              AS day,
    MAX(odometer_km) - MIN(odometer_km)     AS distance_km,
    AVG(speed_kmh)                          AS avg_speed_kmh,
    MAX(speed_kmh)                          AS max_speed_kmh,
    COUNT(*)                                AS position_count
FROM vehicle_positions
GROUP BY tenant_id, vehicle_id, time_bucket('1 day', time)
WITH NO DATA;

-- ─── Refresh policy: hourly, look back 3 days for late-arriving data ──────────
SELECT add_continuous_aggregate_policy('daily_fleet_kpis',
    start_offset      => INTERVAL '3 days',
    end_offset        => INTERVAL '1 hour',
    schedule_interval => INTERVAL '1 hour',
    if_not_exists     => true);

-- ─── Indexes on the materialized view ─────────────────────────────────────────
CREATE INDEX IF NOT EXISTS idx_daily_fleet_kpis_tenant_day
    ON daily_fleet_kpis (tenant_id, day DESC);

CREATE INDEX IF NOT EXISTS idx_daily_fleet_kpis_vehicle_tenant
    ON daily_fleet_kpis (vehicle_id, tenant_id, day DESC);

-- ─── Geofencing DB indexes (run against geofencing_db) ───────────────────────
-- These are plain PostgreSQL indexes — no TimescaleDB required.
-- Idempotent.

-- Primary filter for violation history queries
-- CREATE INDEX IF NOT EXISTS idx_violation_events_tenant_occurred
--     ON violation_events (tenant_id, occurred_at DESC);

-- Secondary filter for per-vehicle queries
-- CREATE INDEX IF NOT EXISTS idx_violation_events_vehicle_tenant
--     ON violation_events (vehicle_id, tenant_id, occurred_at DESC);

-- NOTE: The two commented-out indexes above target geofencing_db.
-- Run them separately connected to geofencing_db:
--   docker exec -it fv-postgres psql -U geofencing_user -d geofencing_db -f this_section.sql
