# FleetVision

![CI](https://github.com/cdgutierrez6/fleetvision/actions/workflows/ci.yml/badge.svg)
![.NET](https://img.shields.io/badge/.NET-8_LTS-blue)
![Angular](https://img.shields.io/badge/Angular-21-red)
![Kafka](https://img.shields.io/badge/Kafka-KRaft_3_nodos-orange)
![License](https://img.shields.io/badge/license-MIT-lightgrey)

> **Plataforma B2B SaaS multi-tenant** de telemetría IoT para flotas comerciales — ingesta de millones de pings GPS/sensores, geofencing en tiempo real, mantenimiento predictivo, analítica geoespacial y capa SaaS completa (billing, planes, onboarding, superadmin).

---

## Architecture

```
[GPS / OBD2 Devices]
        │ gRPC / HTTP
        ▼
[API Gateway — YARP .NET 8]
        │  JWT validation  │  tenant_id propagation
        ▼                  ▼
[Identity & Access]    [Telemetry Ingestion] ──► Kafka telemetry.raw (12 partitions)
[OpenIddict · RBAC]         │                          │
[Tenant Management]    [Redis cache]          ┌────────┴──────────┐
[Billing · Stripe]    last_location           │                   │
[Fleet & Assets]                   [Geofencing & Safety]  [Predictive Maintenance]
[PostGIS · RLS]                    [PostGIS ST_Contains]  [OBD2 · km rules]
                                           │                      │
                                    Kafka events          Kafka events
                                           │                      │
                              [Reporting & Analytics]   [Notifications]
                              [TimescaleDB · CQRS]      [WebSocket · Email]
                                           │
                              [Angular 21 Micro-Frontends]
                              [Native Federation · Nx]
                              [shell / mfe-fleet / mfe-monitoring / ...]
```

**Multi-tenancy:** `tenant_id` en cada tabla + Row-Level Security (RLS) en PostgreSQL. Un tenant nunca accede a datos de otro, incluso ante bugs de aplicación.

---

## Tech Stack

| Capa | Tecnología |
|------|-----------|
| Backend | .NET 8 LTS · Clean Architecture + DDD por servicio |
| API Gateway | YARP (Yet Another Reverse Proxy) |
| Identidad | OpenIddict 5 · JWT · RBAC · refresh tokens |
| Mensajería | Apache Kafka KRaft (3 nodos) · Protobuf · Schema Registry |
| BD Telemetría | TimescaleDB 2 + PostGIS (hypertable por semanas) |
| BD por Servicio | PostgreSQL 16 + PostGIS · Database-per-Service |
| Cache | Redis 7 (AOF persistido) |
| Frontend | Angular 21 · Nx · Native Federation · Signal Forms · Tailwind |
| Mapas | Leaflet · WebSocket tiempo real |
| Observabilidad | OpenTelemetry → Jaeger · Prometheus · Grafana · Loki |
| Resiliencia | Polly · Outbox pattern · DLQ Kafka |
| Billing | Stripe (suscripciones · webhooks) |
| CI/CD | GitHub Actions · Azure Container Apps · Azure Bicep |

---

## Microservicios

| Servicio | Puerto | Descripción |
|---------|--------|-------------|
| `identity` | 5001 | Auth, RBAC, JWT, refresh tokens (OpenIddict) |
| `tenant-management` | 5002 | Onboarding, planes, límites por plan |
| `billing` | 5003 | Stripe, suscripciones, webhooks |
| `fleet-assets` | 5004 | Vehículos, flotas, conductores, geofences (PostGIS) |
| `telemetry` | 5005 | gRPC alto throughput, publica `telemetry.raw` |
| `geofencing` | 5006 | ST_Contains tiempo real, alertas de zona |
| `predictive-maintenance` | 5007 | OBD2, km acumulados, alertas de mantenimiento |
| `reporting` | 5008 | CQRS query model, KPIs, export PDF (QuestPDF) |
| `notifications` | 5009 | SignalR WebSocket, alertas real-time por tenant |
| `gateway` | 5000 | YARP reverse proxy · JWT middleware · security headers |

---

## Quick Start (Dev)

```bash
# Prerequisitos: Docker 24+, Git

git clone https://github.com/cdgutierrez6/fleetvision.git
cd fleetvision

# 1. Configurar entorno
cp .env.example .env
# Editar .env — completar POSTGRES_PASSWORD, TIMESCALE_PASSWORD,
# REDIS_PASSWORD, GRAFANA_ADMIN_PASSWORD

# 2. Generar Kafka Cluster ID (obligatorio para KRaft)
docker run --rm confluentinc/cp-kafka:7.7.1 kafka-storage random-uuid
# Copiar el output en .env → KAFKA_CLUSTER_ID=<valor>

# 3. Levantar infraestructura
docker compose -f docker-compose.dev.yml up -d

# 4. Verificar que todo está verde
docker compose -f docker-compose.dev.yml ps
```

Servicios disponibles tras el arranque:

| UI | URL |
|----|-----|
| Grafana | http://localhost:3000 |
| Jaeger | http://localhost:16686 |
| Prometheus | http://localhost:9090 |
| Schema Registry | http://localhost:8081 |

---

## Environment Variables

Ver [`.env.example`](.env.example) para todas las variables con descripción.

**Mínimo requerido para arrancar la infraestructura:**

```env
POSTGRES_PASSWORD=<min_32_chars>
TIMESCALE_PASSWORD=<min_32_chars>
REDIS_PASSWORD=<min_32_chars>
GRAFANA_ADMIN_PASSWORD=<cualquiera_para_dev>
KAFKA_CLUSTER_ID=<output_de_kafka_storage_random_uuid>
```

---

## Project Structure

```
fleetvision/
├── services/                    # Microservicios .NET 8
│   ├── identity/                # Clean Architecture: Domain/Application/Infrastructure/API
│   ├── tenant-management/
│   ├── billing/
│   ├── fleet-assets/
│   ├── telemetry/
│   ├── geofencing/
│   ├── predictive-maintenance/
│   ├── reporting/
│   ├── notifications/
│   └── gateway/                 # YARP API Gateway
├── frontend/                    # Nx workspace Angular 21
│   ├── apps/
│   │   ├── shell/               # Host MFE — router, auth guard
│   │   ├── mfe-fleet/           # Gestión de vehículos y conductores
│   │   ├── mfe-monitoring/      # Mapa tiempo real + WebSocket
│   │   ├── mfe-reports/         # KPIs y analítica
│   │   ├── mfe-alerts/          # Notificaciones
│   │   ├── mfe-admin/           # Superadmin de tenants
│   │   └── mfe-billing/         # Planes y facturación
│   └── libs/shared/             # Design system, interceptores, auth
├── infra/
│   ├── db/                      # Scripts SQL de inicialización + reporting views
│   ├── prometheus/              # prometheus.yml
│   ├── loki/                    # loki-config.yml
│   ├── grafana/                 # Datasources y dashboards provisionados
│   ├── bicep/                   # IaC Azure Container Apps
│   ├── scripts/                 # smoke-test.ps1
│   └── k6/                      # Load test — ramping 10k pings/s
├── proto/                       # Definiciones Protobuf compartidas
├── docs/                        # Runbook operativo
├── .github/workflows/           # CI/CD (1 pipeline por servicio/MFE + cd-azure.yml)
├── docker-compose.dev.yml       # Infraestructura de desarrollo
└── CHECKLIST.md                 # Estado detallado por fase y RFC
```

---

## Status

| Fase | Descripción | Estado |
|------|-------------|--------|
| F0 | Infraestructura base (Docker Compose, Kafka KRaft, TimescaleDB, PostGIS, Redis, OTel) | ✅ Completo |
| F1 | Identity & Access + API Gateway (OpenIddict, JWT, RBAC, 34 tests) | ✅ Completo |
| F2 | Tenant Management + Billing Stripe (outbox, webhooks, 53 tests) | ✅ Completo |
| F3 | Fleet & Assets CRUD PostGIS (65 tests) | ✅ Completo |
| F4 | Telemetry gRPC + KafkaRelayWorker + TimescaleDB (62 tests) | ✅ Completo |
| F5 | Geofencing ST_Contains + ViolationOutbox (34 tests) | ✅ Completo |
| F6 | Predictive Maintenance OBD2 + Redis odometer (36 tests) | ✅ Completo |
| F7 | Reporting CQRS + TimescaleDB + QuestPDF (15 tests) | ✅ Completo |
| F8 | Notifications SignalR real-time (16 tests) | ✅ Completo |
| F9 | Angular 21 frontend — 7 MFEs Nx Native Federation | ✅ Completo |
| F10 | OTel todos los servicios + Polly + DLQ Kafka | ✅ Completo |
| F11 | GitHub Actions CI/CD + Azure Bicep IaC + cd-azure.yml staging→prod | ✅ Completo |
| F12 | Security headers, OWASP audit, k6 load test, runbook, smoke tests 37/37 PASSED | ✅ Completo |

---

## License

MIT — Cristian Gutierrez © 2026
