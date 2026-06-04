-- ============================================================
-- FleetVision — TimescaleDB init script
-- Activa extensiones y configura la base de telemetría.
-- ============================================================

CREATE EXTENSION IF NOT EXISTS timescaledb;
CREATE EXTENSION IF NOT EXISTS postgis;
CREATE EXTENSION IF NOT EXISTS postgis_topology;
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- La hypertable se crea en RFC-008 (Telemetry Persistence Consumer)
-- cuando se aplica la migración del servicio de telemetría.
-- Este script solo garantiza que las extensiones estén disponibles.

SELECT extname, extversion
FROM pg_extension
WHERE extname IN ('timescaledb', 'postgis', 'uuid-ossp')
ORDER BY extname;
