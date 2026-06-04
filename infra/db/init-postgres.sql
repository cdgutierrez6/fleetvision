-- ============================================================
-- FleetVision — PostgreSQL init script
-- Crea las bases de datos por servicio y activa las extensiones.
-- Ejecutado automáticamente al primer arranque del contenedor.
-- ============================================================

-- Extensiones en la base por defecto (para disponer de uuid_generate_v4)
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS postgis;
CREATE EXTENSION IF NOT EXISTS postgis_topology;

-- ─── Identity Service ────────────────────────────────────────
CREATE DATABASE identity_db;
\c identity_db
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS pgcrypto;

-- ─── Tenant Management Service ───────────────────────────────
\c postgres
CREATE DATABASE tenant_mgmt_db;
\c tenant_mgmt_db
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- ─── Billing Service ─────────────────────────────────────────
\c postgres
CREATE DATABASE billing_db;
\c billing_db
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- ─── Fleet & Assets Service ──────────────────────────────────
\c postgres
CREATE DATABASE fleet_db;
\c fleet_db
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS postgis;
CREATE EXTENSION IF NOT EXISTS postgis_topology;

-- ─── Geofencing & Safety Service ─────────────────────────────
\c postgres
CREATE DATABASE geofencing_db;
\c geofencing_db
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS postgis;
CREATE EXTENSION IF NOT EXISTS postgis_topology;

-- ─── Predictive Maintenance Service ──────────────────────────
\c postgres
CREATE DATABASE maintenance_db;
\c maintenance_db
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- ─── Reporting & Analytics Service ───────────────────────────
\c postgres
CREATE DATABASE reporting_db;
\c reporting_db
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS postgis;

-- ─── Notifications Service ───────────────────────────────────
\c postgres
CREATE DATABASE notifications_db;
\c notifications_db
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- Volver a postgres para confirmar
\c postgres
SELECT datname FROM pg_database WHERE datname LIKE '%_db' ORDER BY datname;
