-- ============================================================
-- FleetVision — TimescaleDB init script
-- Activa extensiones, crea hypertable de telemetría,
-- outbox de Kafka y vista materializada de estadísticas.
-- Idempotente: seguro de ejecutar múltiples veces.
-- ============================================================

-- ─── Extensiones ─────────────────────────────────────────────
CREATE EXTENSION IF NOT EXISTS timescaledb;
CREATE EXTENSION IF NOT EXISTS postgis;
CREATE EXTENSION IF NOT EXISTS postgis_topology;
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- ─── vehicle_positions — Hypertable principal ────────────────
-- Almacena cada ping GPS de cada vehículo.
-- Particionada automáticamente por semanas via TimescaleDB.
CREATE TABLE IF NOT EXISTS vehicle_positions (
    time             TIMESTAMPTZ      NOT NULL,
    vehicle_id       UUID             NOT NULL,
    tenant_id        UUID             NOT NULL,
    driver_id        UUID,                           -- NULL si no hay asignación activa
    latitude         DOUBLE PRECISION NOT NULL,
    longitude        DOUBLE PRECISION NOT NULL,
    speed_kmh        DOUBLE PRECISION,
    heading_deg      SMALLINT,                       -- 0-359 grados
    accuracy_m       DOUBLE PRECISION,               -- Precisión GPS en metros
    hdop             DOUBLE PRECISION,               -- Dilución horizontal de precisión
    satellite_count  SMALLINT,
    fuel_pct         DOUBLE PRECISION,               -- 0-100
    engine_on        BOOLEAN,
    obd2_codes       TEXT[],                         -- Ej: ARRAY['P0300', 'P0171']
    odometer_km      DOUBLE PRECISION,               -- Odómetro acumulado del vehículo (km)
    raw_payload      JSONB                           -- Payload completo del dispositivo
);

-- Convertir a hypertable (particionamiento semanal automático)
SELECT create_hypertable(
    'vehicle_positions',
    'time',
    chunk_time_interval => INTERVAL '1 week',
    if_not_exists       => TRUE
);

-- Índices de acceso más frecuentes
-- Query: "últimas N posiciones de un vehículo"
CREATE INDEX IF NOT EXISTS idx_vp_vehicle_time
    ON vehicle_positions (vehicle_id, time DESC);

-- Query: "todos los pings de un tenant en un rango de tiempo" (dashboard)
CREATE INDEX IF NOT EXISTS idx_vp_tenant_time
    ON vehicle_positions (tenant_id, time DESC);

-- Query: "pings activos (motor encendido) de un tenant"
CREATE INDEX IF NOT EXISTS idx_vp_tenant_engine
    ON vehicle_positions (tenant_id, time DESC)
    WHERE engine_on = TRUE;

-- ─── outbox_events — Cola de publicación Kafka ───────────────
-- Garantiza entrega at-least-once: el OutboxRelayWorker publica
-- a Kafka y marca published_at. Sin esta tabla, un crash entre
-- la escritura en TimescaleDB y el publish a Kafka perdería el evento.
CREATE TABLE IF NOT EXISTS outbox_events (
    id           UUID         PRIMARY KEY DEFAULT uuid_generate_v4(),
    topic        VARCHAR(100) NOT NULL,              -- Ej: 'telemetry.raw'
    partition_key VARCHAR(36) NOT NULL,              -- vehicle_id — para ordering
    payload      BYTEA        NOT NULL,              -- VehiclePositionEvent serializado (Protobuf)
    schema_id    INTEGER      NOT NULL,              -- ID del Schema Registry
    created_at   TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    published_at TIMESTAMPTZ,                        -- NULL = pendiente de publicar
    retry_count  SMALLINT     NOT NULL DEFAULT 0,
    last_error   TEXT                                -- Último error del relay worker
);

-- Índice parcial: solo filas pendientes (published_at IS NULL).
-- El OutboxRelayWorker hace polling sobre este índice cada 500ms.
CREATE INDEX IF NOT EXISTS idx_outbox_pending
    ON outbox_events (created_at ASC)
    WHERE published_at IS NULL;

-- ─── hourly_vehicle_stats — Vista materializada continua ─────
-- Estadísticas horarias por vehículo para reportes de eficiencia.
-- TimescaleDB refresca automáticamente según la policy configurada.
CREATE MATERIALIZED VIEW IF NOT EXISTS hourly_vehicle_stats
WITH (timescaledb.continuous) AS
SELECT
    time_bucket('1 hour', time)  AS bucket,
    vehicle_id,
    tenant_id,
    AVG(speed_kmh)               AS avg_speed_kmh,
    MAX(speed_kmh)               AS max_speed_kmh,
    MIN(speed_kmh)               AS min_speed_kmh,
    COUNT(*)                     AS ping_count,
    BOOL_OR(engine_on)           AS was_engine_on,
    AVG(fuel_pct)                AS avg_fuel_pct
FROM vehicle_positions
WHERE speed_kmh IS NOT NULL
GROUP BY bucket, vehicle_id, tenant_id
WITH NO DATA;

-- Policy de refresco: cada hora, desde 2h atrás hasta 1h atrás.
-- Evita refrescar datos muy recientes (chunks abiertos) que aún cambian.
SELECT add_continuous_aggregate_policy(
    'hourly_vehicle_stats',
    start_offset      => INTERVAL '2 hours',
    end_offset        => INTERVAL '1 hour',
    schedule_interval => INTERVAL '1 hour',
    if_not_exists     => TRUE
);

-- ─── Verificación ────────────────────────────────────────────
SELECT
    hypertable_name,
    num_chunks,
    compression_enabled
FROM timescaledb_information.hypertables
WHERE hypertable_name = 'vehicle_positions';

SELECT extname, extversion
FROM pg_extension
WHERE extname IN ('timescaledb', 'postgis', 'uuid-ossp')
ORDER BY extname;
