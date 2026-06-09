# FleetVision — Checklist de Progreso

Estado general: **DEUDAS TÉCNICAS CERRADAS (2026-06-09). TenantRlsInterceptor is_local=true en 4 servicios + OBD2Code regex + CompleteRecord audit log. Pendiente: k6 load test (k6 no instalado) + Stripe keys en .env.**

---

## F0 — Infraestructura Base

### RFC-001 — Bootstrap de Infraestructura ✅ COMPLETADO

**Alcance:** Monorepo, Docker Compose completo, scripts de DB, observabilidad, CI base.

**Archivos entregados:**
- `docker-compose.dev.yml` — Postgres 16, TimescaleDB 2.x, Kafka KRaft 3 nodos, Schema Registry, Redis, Jaeger, Prometheus, Grafana, Loki
- `infra/db/init-postgres.sql` — Creación de todas las DBs del sistema (identity_db, tenant_db, billing_db, fleet_db, telemetry_db, notifications_db)
- `infra/db/init-timescale.sql` — Extensiones TimescaleDB + hypertable telemetry
- `infra/prometheus/prometheus.yml` — Scrape configs por servicio
- `infra/grafana/provisioning/datasources/datasources.yml` — Prometheus + Loki + Jaeger
- `infra/grafana/provisioning/dashboards/dashboards.yml` — Dashboard provisioning
- `infra/loki/loki-config.yml` — Retención de logs
- `infra/scripts/kafka-init.sh` — Creación de topics al inicio del cluster
- `.env.example` — Todas las variables requeridas documentadas
- `.gitignore`, `.nvmrc` (Node 22), `.node-version`
- `README.md`, `CHECKLIST.md`, `CLAUDE.md`
- Repo GitHub: `cdgutierrez6/fleetvision`

**Criterios de Aceptación (código):** ✅ Todos los archivos creados y commiteados.
**Criterios de Aceptación (runtime):** Pendientes de validar con `docker compose up`.

---

- [x] RFC-001: Bootstrap de infraestructura — todos los archivos creados
- [x] `.env` local completado con contraseñas reales de dev
- [x] `KAFKA_CLUSTER_ID` generado y añadido a `.env` (`eRErjPr1QVmT_O92B3lA4g`)
- [ ] `docker compose -f docker-compose.dev.yml up -d` levanta sin errores
- [ ] PostgreSQL health check verde — todas las DBs creadas
- [ ] TimescaleDB health check verde — extensiones activas
- [ ] Kafka 3 brokers health check verde
- [ ] Schema Registry accesible en `:8081`
- [ ] Redis health check verde — responde a PING
- [ ] Kafka topics creados (verificar con `kafka-topics --list`)
- [ ] Jaeger UI accesible en `http://localhost:16686`
- [ ] Grafana accesible en `http://localhost:3000`
- [ ] Prometheus scrape activo en `http://localhost:9090`
- [ ] Loki healthy en `http://localhost:3100/ready`
- [ ] Datasources Prometheus + Loki + Jaeger configurados en Grafana

---

## F1 — Identity & Access Service

- [x] RFC-002: Dominio + JWT (HmacSha256) + Argon2 password hasher + RBAC (roles: SuperAdmin/Admin/FleetManager/Driver)
- [x] RFC-003: API Gateway YARP + TenantPropagationMiddleware
- [x] Tests unitarios: 21 domain + 13 application = 34 tests pasando
- [x] Health checks `/health` configurados en Identity y Gateway
- [x] Índices en `users(email)` y `users(tenant_id)` — RLS via `UserConfiguration`
- [x] Migración EF Core `InitialCreate` generada
- [x] Refresh tokens con rotación + SHA256 hash en DB

---

## F2 — Tenant Management + Billing

- [x] RFC-004: Tenant Management — CRUD tenants, planes (Free/Starter/Professional/Enterprise), `SetTenantActiveStatus`, `UpdateTenantPlan`
- [x] `TenantRlsInterceptor` — inyecta `SET app.tenant_id` en cada transacción
- [x] `TenantContextMiddleware` — extrae `tenant_id` del JWT claim
- [x] Tests: 13 domain + 11 application = 24 tests pasando
- [x] RFC-005: Billing + Stripe webhooks (services/billing/ — 4 capas completas)
- [x] Evento `tenant.provisioned` en Kafka (BillingRelayWorker → outbox)
- [x] Evento `billing.subscription.changed` en Kafka (BillingRelayWorker → outbox)
- [x] `TenantProfile.SetPlanByBilling()` — permite downgrades desde Billing
- [x] Endpoint interno `PUT /internal/tenants/{id}/plan` en TenantManagement
- [x] YARP Gateway rutas `/api/billing/*` + webhook anonymous
- [x] `ci-billing.yml` workflow
- [x] Security remediations: FixedTimeEquals, Serilog PII, sessionUrl guard, idempotency, generic errors, rate limiter, audit log, BaseUrl validation
- [x] Tests: 7 domain + 15 application + 7 API.Tests = 29 tests — RFC-005 APROBADO
- [x] `dotnet ef migrations add InitialCreate` en services/billing/ — migración `20260609160731_InitialCreate.cs` generada (tablas: subscriptions, billing_outbox_events, plan_change_audit)
- [x] `INTERNAL_API_KEY` agregado a `.env` y wired en docker-compose (tenant-management + billing)
- [ ] Variables Stripe en `.env` (STRIPE_SECRET_KEY, STRIPE_WEBHOOK_SECRET, STRIPE_PRICE_*_ID) — pendiente keys reales del dashboard
- [x] `Microsoft.EntityFrameworkCore.Design` agregado al billing API .csproj
- [x] Billing Program.cs — HARDENING H-01 aplicado (SigningKey length >= 32)
- [x] `.env.example` puertos corregidos: BILLING_PORT=5003, FLEET_ASSETS_PORT=5004, GEOFENCING_PORT=5006 (estaban invertidos)

---

## F3 — Fleet & Assets

- [x] RFC-006: CRUD vehículos, flotas, conductores, geofences (PostGIS polygons)
- [x] PostGIS índices GIST en columnas geométricas
- [x] RLS multi-tenant via tenant_id en todas las entidades
- [x] Tests: 29 domain + 36 application = 65 tests pasando
- [x] Migraciones EF Core generadas

---

## F4 — Telemetry Ingestion

- [x] RFC-007: gRPC `TelemetryGrpcService` + `TenantAuthInterceptor` (JWT en metadata)
- [x] RFC-008: `KafkaRelayWorker` (hosted service) → `TelemetryWriter` → TimescaleDB hypertable
- [x] `PositionCache` Redis — última posición por vehículo (TTL)
- [x] `TelemetryRepository` — queries a TimescaleDB
- [x] Tests: 25 domain + 37 application = 62 tests pasando
- [x] gRPC reflection activa en desarrollo

---

## F5 — Geofencing & Safety

- [x] RFC-009: Consumer Kafka + `ST_Contains` PostGIS + generación de `ViolationEvent`
- [x] Tipos de violación: SpeedExceeded, EnteredForbiddenZone, ExitedAllowedZone, OutsideSchedule
- [x] Tests: 21 application + 13 domain = 34 tests pasando
- [ ] Latencia alerta < 2 segundos desde ping (pendiente validar con Docker)

---

## F6 — Predictive Maintenance

- [x] RFC-021: Servicio completo implementado — Clean Architecture 4 capas
  - Domain: MaintenanceRecord, OdometerSnapshot, OBD2Code, 4 reglas, MaintenanceRuleEngine (Domain Service)
  - Application: MaintenanceOrchestrator, CompleteMaintenanceCommand, GetMaintenanceRecordsQuery
  - Infrastructure: TelemetryConsumer (Kafka + DLQ), OdometerCache (Redis INCRBYFLOAT + dedup por offset), MaintenanceOutboxEnqueuer, MaintenanceRelayWorker (FOR UPDATE SKIP LOCKED)
  - API: Program.cs con OTel + JWT + rate limiting por JWT claim + security headers + health checks liveness/readiness
  - 36 tests: OdometerRuleTests (5) + OBD2RuleTests (6) + MaintenanceRuleEngineTests (6) + TimeBasedRuleTests (7) + MaintenanceRecordTests (4) + OrchestratorTests (4) + CompleteMaintenanceHandlerTests (4)
  - Gateway YARP ruta /api/maintenance/* → predictive-maintenance:8080 añadida
  - DLQ topics maintenance.scheduled.dlq + vehicle.alert.dlq en kafka-init.sh
- [x] Security audit: 3 blockers corregidos (TenantContextMiddleware fail-closed, RLS NULLIF, rate limiting por JWT)
- [x] Tech-Lead review: APROBADO
- [x] Deuda técnica RFC-026: OBD2Code — regex `^[PCBU][0-9]{4}$` añadida en `TryParse` (rechaza códigos malformados)
- [x] Deuda técnica RFC-026: CompleteMaintenanceCommand — audit log estructurado `AUDIT maintenance_completed` vía ILogger
- [ ] Validar runtime: consumer procesa telemetry.raw y genera alertas
- [ ] Latencia alerta < 3 segundos desde ping

---

## F7 — Reporting & Analytics

- [x] RFC-011: CQRS + TimescaleDB Continuous Aggregates + QuestPDF export — APROBADO por tech-lead
  - Servicio completo en `services/reporting/` — 4 capas Clean Architecture
  - 5 endpoints: fleet-kpis, violations-summary, vehicle-history, fleet-status, export/pdf
  - 3 NpgsqlDataSource keyed: telemetry_db + geofencing_db + fleet_db
  - Rate limiting 5/min en PDF export, Jwt:SigningKey fail-fast
  - 15 tests unitarios (handlers + security boundary tests)
  - Gateway YARP route `/api/reports/*` → reporting:8080
  - ReportingStore + ReportingService en shared/data-access
  - mfe-reports actualizado para datos históricos reales

---

## F8 — Notifications

- [x] RFC-010: `ViolationHub` SignalR (WebSocket, query-string JWT para handshake)
- [x] `ViolationKafkaConsumer` → broadcast a grupos por tenant_id
- [x] JWT Bearer con eventos `OnMessageReceived` para SignalR
- [x] Tests: 16 pasando (ViolationHub + KafkaConsumer broadcast)
- [x] `FleetVision.Notifications.sln` creado

---

## F9 — Frontend Angular 21

- [x] RFC-013: Shell Nx + Native Federation — routes, auth guard, token interceptor, login, header, sidenav
- [x] Shared libs: `@fleetvision/shared/models`, `@fleetvision/shared/data-access`, `@fleetvision/shared/ui`
- [x] `AuthStore` (NgRx Signal Store) + `SignalRService` (real-time violations)
- [x] `VehiclesStore`, `GeofencesStore`, `ViolationsStore`
- [x] RFC-014: `mfe-fleet` — `DashboardComponent` (tabla vehículos, KPIs, drawer detalle, alertas)
- [x] RFC-015: `mfe-monitoring` — `MapComponent` (Leaflet, markers vehículos, polígonos geofences, violations)
- [x] `mfe-alerts` — `FeedComponent` (filtros, live/history tabs, conexión SignalR, skeleton loaders)
- [x] `mfe-admin` — Panel de administración de tenants (TenantsStore + TenantsService + AdminComponent)
- [x] `mfe-reports` — Reporting & Analytics dashboard (KPIs flota, violaciones por tipo, distribución status)
- [x] `mfe-billing` — Gestión de planes y suscripción (BillingStore + BillingService + BillingComponent + Shell wired)
- [x] Shell route `/admin` agregada + sidenav item "Administración"
- [x] Script `serve:mfe-admin` en package.json (port 4204)
- [x] Shell route `/reports` agregada + sidenav item "Reportes"
- [x] Script `serve:mfe-reports` en package.json (port 4205)

---

## F10 — Observabilidad + Resiliencia

- [x] RFC-022: OTel configurado en TODOS los servicios (gateway, identity, tenant-management, billing, fleet-assets, telemetry, geofencing, reporting, notifications, predictive-maintenance)
- [x] RFC-023: Polly `AddStandardResilienceHandler()` en billing, geofencing, fleet-assets (servicios con HTTP clients)
- [x] DLQ topics completos: telemetry.raw.dlq, geofence.violation.dlq, driver.behavior.alert.dlq, maintenance.scheduled.dlq, vehicle.alert.dlq

---

## F11 — CI/CD + Azure

- [x] GitHub Actions workflows por servicio (ci-identity, ci-gateway, ci-tenant-management, ci-fleet-assets, ci-geofencing, ci-telemetry, ci-notifications)
- [x] ci-predictive-maintenance.yml — nuevo workflow añadido
- [x] ci-reporting.yml — ya existía (verificado)
- [x] ci-frontend.yml — nuevo workflow Nx build + lint + test
- [x] RFC-025: Azure Bicep IaC completo (main.bicep + 5 módulos + parámetros dev/prod)
- [x] cd-azure.yml — deploy staging → prod con aprobación manual + rollback automático

---

## F12 — Hardening + Launch

- [x] RFC-026: Security headers en Gateway (CSP, HSTS, X-Frame-Options, Referrer-Policy, Permissions-Policy)
- [x] Security headers en predictive-maintenance API (X-Content-Type-Options, X-Frame-Options, HSTS)
- [x] Polly circuit breaker en todos los HTTP clients (billing, geofencing, fleet-assets)
- [x] k6 load test script: infra/k6/telemetry-load-test.js (ramping-arrival-rate → 10k/s × 5min)
- [x] Runbook operativo: docs/runbook.md (5 incidentes comunes, backups, escalado, métricas Grafana)
- [x] `infra/scripts/smoke-test.ps1` — script PowerShell de smoke tests E2E creado (infra health + service health + auth flow + kafka topics)
- [x] Levantar infra: `docker compose -f docker-compose.dev.yml up -d` — 22 containers healthy
- [x] Correr smoke tests: `.\infra\scripts\smoke-test.ps1` — **37/37 PASSED** (infra + health checks + auth E2E + Kafka topics)
- [x] EF Core migrations: billing aplica `MigrateAsync()` automáticamente en Development al arrancar
- [ ] k6: ejecutar y verificar que 10k pings/seg pass p95 < 500ms (runtime pendiente)
- [ ] Backup TimescaleDB restaurable verificado (runtime pendiente)
- [x] OWASP Top 10: Security audit completo — 4 BLOCKERs + 4 VULNs + 2 adicionales en tech-lead review

### Tech-Lead Final Review (2026-06-09) — APROBADO

**BLOCKERs encontrados y corregidos:**
- B-TL-01 FIXED: `BillingRelayWorker` — `await t` en `finally` nunca capturaba errores Kafka; eventos marcados como publicados aunque fallaran (pérdida silenciosa de datos)
- B-TL-02 FIXED: `MaintenanceRelayWorker` — mismo bug, `await t` nunca llamado en el acumulador de resultados

**VULNs adicionales corregidos:**
- V-TL-01 FIXED: Gateway `Program.cs` — `{Properties:j}` en Serilog (PII en logs — V-02 había omitido el gateway y predictive-maintenance)
- V-TL-02 FIXED: `predictive-maintenance/Program.cs` — `{Properties:j}` también omitido en el audit anterior

**HARDENINGs aplicados:**
- H-TL-01: Gateway `Program.cs` — JWT SigningKey length >= 32 (solo tenía null check)

**Correcciones de config:**
- Gateway `appsettings.Development.json` — puertos corregidos: fleet-assets 5003→5004, geofencing 5004→5006
  - B-01 FIXED: TenantContextMiddleware fail-closed en fleet-assets, geofencing, tenant-management, billing
  - B-02 FIXED: Telemetry ValidateAudience=false → symmetric key auth igual al resto de servicios
  - B-03 FIXED: GET /tenants/{id}/limits requiere X-Internal-Key (FixedTimeEquals)
  - B-04 FIXED: Identity rate limiter aplicado a /auth/login y /auth/register vía [EnableRateLimiting]
  - V-01 FIXED: Reporting TenantContextMiddleware ya no acepta X-Tenant-Id header como fallback
  - V-02 FIXED: {Properties:j} eliminado de Serilog en 5 servicios (PII en logs)
  - V-03 FIXED: Notifications Kafka consumer DLQ agregado para mensajes corruptos
  - V-04 FIXED: Billing TenantContextMiddleware refactorizado (dead code → fail-closed sobre JWT)
  - EXTRA FIXED: kafka-init.sh — topic renombrado a geofencing.violations (inconsistencia con código)
