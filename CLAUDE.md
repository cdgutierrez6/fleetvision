# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

---

## Development Commands

### Infrastructure

```bash
# Start all infrastructure (required before running any service)
docker compose -f docker-compose.dev.yml up -d

# Status check
docker compose -f docker-compose.dev.yml ps

# Stop (preserve volumes)
docker compose -f docker-compose.dev.yml down

# Full reset (destroys all data)
docker compose -f docker-compose.dev.yml down -v
```

### .NET Services

Each service has its own `.sln` file. Commands must target the `.sln` or `.csproj` directly.

```bash
# Build a service
dotnet build services/identity/src/FleetVision.Identity.API/FleetVision.Identity.API.csproj -c Release

# Run a service locally (requires infra up)
dotnet run --project services/identity/src/FleetVision.Identity.API

# Run all tests for a service
dotnet test services/notifications/FleetVision.Notifications.sln \
  --logger "console;verbosity=minimal"

# Run a single test class
dotnet test services/fleet-assets/FleetVision.FleetAssets.sln \
  --filter "FullyQualifiedName~VehicleTests" \
  --logger "console;verbosity=minimal"

# EF Core migrations (run from inside services/<name>/)
dotnet ef migrations add <MigrationName> \
  --project src/FleetVision.Identity.Infrastructure \
  --startup-project src/FleetVision.Identity.API
dotnet ef database update \
  --project src/FleetVision.Identity.Infrastructure \
  --startup-project src/FleetVision.Identity.API
```

CI quality gate: line coverage ≥ 70% enforced via Cobertura XML in every workflow that sets `has-tests: true`.

### Frontend (Nx workspace — `frontend/`)

```bash
cd frontend

# Install dependencies
npm install

# Serve all MFEs (shell + all remotes in parallel)
npm run serve:all
# Or serve individually:
npm run serve:shell           # port 4200
npm run serve:mfe-fleet       # port 4201
npm run serve:mfe-alerts      # port 4202
npm run serve:mfe-monitoring  # port 4203
npm run serve:mfe-admin       # port 4204

# Build all apps
npm run build

# Build for production
npm run build:prod

# Run Nx target for a single app
npx nx build mfe-fleet
npx nx lint mfe-fleet
npx nx test mfe-fleet
```

### Useful diagnostics

```bash
# Verify Kafka topics
docker exec fv-kafka-1 kafka-topics --bootstrap-server localhost:9092 --list

# Check Schema Registry subjects
curl http://localhost:8081/subjects

# Connect to PostgreSQL (services DB)
docker exec -it fv-postgres psql -U fv_admin -d identity_db

# Connect to TimescaleDB
docker exec -it fv-timescaledb psql -U telemetry_user -d telemetry_db

# Redis ping
docker exec fv-redis redis-cli -a $REDIS_PASSWORD PING
```

---

## Architecture

### Request flow

```
[Device] → gRPC → [Telemetry :5005]
                        │ INSERT vehicle_positions + outbox_events (atomic tx)
                        │ KafkaRelayWorker polls outbox → publishes telemetry.raw (12 partitions)
                        ▼
                   [Kafka telemetry.raw]
                   /                   \
[Geofencing :5006]                [Predictive Maintenance :5007]
 ST_Contains PostGIS               OBD2 rules + Redis odometer
 → ViolationOutboxEnqueuer         → (same outbox pattern)
 → ViolationRelayWorker
 → Kafka geofencing.violations
         │
[Notifications :5009]
 ViolationKafkaConsumer
 → SignalR broadcast by tenant_id
         │
[Angular Shell :4200] ← WebSocket
```

HTTP requests from Angular go through:
```
[Angular] → HTTP → [Gateway :5000 YARP]
                        │ JWT validation
                        │ TenantPropagationMiddleware → header X-Tenant-Id
                        ▼
                  [Target service]
                        │ TenantContextMiddleware reads X-Tenant-Id
                        │ TenantRlsInterceptor → SET LOCAL app.tenant_id per tx
```

### Multi-tenancy (Row-Level Security)

Every table with tenant data has a PostgreSQL RLS policy keyed on `app.tenant_id`. The EF Core interceptor `TenantRlsInterceptor` runs `SET LOCAL app.tenant_id = '<uuid>'` at the start of every transaction. Add this to any new service that handles tenant-scoped data — it is the first thing wired in `DependencyInjection.cs`.

### Outbox pattern (all event publishers)

Publishers never call Kafka directly. They write to an `*_outbox_events` table in the same EF Core transaction as the domain change. A `*RelayWorker` (`BackgroundService`) polls that table using `FOR UPDATE SKIP LOCKED` via a raw `NpgsqlDataSource` and publishes to Kafka. The `IViolationPublisher` interface abstracts this so Application layer has no Kafka dependency.

### Kafka consumers

All consumers use manual offset commit (`EnableAutoCommit = false`) after successful processing. Failed deserialization or processing sends the raw bytes to `<topic>.dlq` via an idempotent producer (`Acks.All`). Consumer group IDs follow the pattern `<service-name>-service`.

### Service structure (Clean Architecture)

Each service under `services/<name>/` has the same four-project layout:

```
FleetVision.<Name>.Domain         — entities, value objects, domain events (no external deps)
FleetVision.<Name>.Application    — MediatR handlers, FluentValidation validators, interfaces
FleetVision.<Name>.Infrastructure — EF Core DbContext, Kafka, Redis, HTTP clients
FleetVision.<Name>.API            — Program.cs, controllers, middleware, health checks
```

`DependencyInjection.cs` in Infrastructure wires all registrations. `Program.cs` in API calls `AddInfrastructure(configuration)` then configures Serilog, MediatR + FluentValidation pipeline behaviour, JWT bearer auth, rate limiting, OTel tracing, and health checks.

Telemetry service exception: it uses raw `NpgsqlDataSource` instead of EF Core because TimescaleDB hypertable inserts require bulk-optimised SQL.

### Frontend (Angular 21 + Native Federation)

The `shell` app (port 4200) owns routing, auth, and the sidenav. MFE components load lazily via `loadRemoteModule` from `@angular-architects/native-federation`.

Each MFE:
- `federation.config.ts` exposes one component (e.g. `'./Dashboard': './src/app/dashboard/dashboard.component.ts'`)
- Runs on a fixed port (4201–4204+); shell `federation.config.ts` must list every remote
- Bootstrap sequence: `main.ts` → `initFederation()` → dynamic `import('./bootstrap')` → `bootstrapApplication`

Shared state lives in `@fleetvision/shared/data-access` (NgRx Signal Stores). All stores follow:

```typescript
signalStore(
  { providedIn: 'root' },
  withEntities<T>(),
  withState({ isLoading: false, isSaving: false, error: null as string | null }),
  withComputed(({ entities }) => ({ /* derived signals */ })),
  withMethods((store, service = inject(MyService)) => ({ /* async methods */ }))
)
```

Angular patterns enforced everywhere:
- `ChangeDetectionStrategy.OnPush` on every component
- `@if` / `@for` / `@defer` control flow (never `*ngIf` / `*ngFor`)
- `input.required<T>()` for signal-based inputs; `inject()` instead of constructor injection
- CSS variables: `--fv-primary: #1E3A5F`, `--fv-accent: #00BFA5`, `--fv-bg: #F5F7FA`, `--fv-border: #E0E4EA`

---

## Architecture Decisions

### TimescaleDB (not plain PostgreSQL) for telemetry
PostgreSQL without the extension collapses at millions of GPS pings — no auto time-partitioning, B-Tree index degradation, inefficient materialized view refreshes. TimescaleDB adds hypertables (auto-partitioned by week), native compression, and optimised temporal window functions.

### KRaft (no ZooKeeper)
Kafka 3.8 deprecated ZooKeeper. KRaft is standard since 2024; using ZooKeeper in a new project is tech debt from day one.

### OpenIddict (not IdentityServer4 / Duende)
IdentityServer4 unsupported since January 2023. Duende requires a commercial licence for production. OpenIddict is MIT-licensed and actively maintained with native ASP.NET Core integration.

### YARP (not Ocelot)
Ocelot has had no significant releases since 2022. YARP is Microsoft's actively maintained reverse proxy with better .NET 8 integration.

---

## Dev Port Map

| Component | Port |
|-----------|------|
| YARP Gateway | 5000 |
| Identity | 5001 |
| Tenant Management | 5002 |
| Billing | 5003 |
| Fleet & Assets | 5004 |
| Telemetry Ingestion | 5005 |
| Geofencing | 5006 |
| Predictive Maintenance | 5007 |
| Reporting | 5008 |
| Notifications | 5009 |
| PostgreSQL | 5434 |
| TimescaleDB | 5435 |
| Kafka brokers | 9092–9094 |
| Schema Registry | 8081 |
| Redis | 6380 |
| Jaeger UI | 16686 |
| OTLP gRPC / HTTP | 4317 / 4318 |
| Prometheus | 9090 |
| Grafana | 3000 |
| Loki | 3100 |
| Angular shell | 4200 |
| mfe-fleet | 4201 |
| mfe-alerts | 4202 |
| mfe-monitoring | 4203 |
| mfe-admin | 4204 |

---

## Architecture Decisions (continued)

### telemetry-ingestion service descartado (YAGNI — 2026-06-06)
El directorio `services/telemetry-ingestion/` fue eliminado. El servicio `telemetry` (5005) ya implementa gRPC + KafkaRelayWorker con separación correcta (ingesta → outbox → Kafka). Extraer si throughput sostenido supera 50k pings/s.

### Predictive Maintenance — MaintenanceRuleEngine como Domain Service
La regla de evaluación de mantenimiento vive en Domain layer (sin deps de infraestructura). El Application layer tiene `MaintenanceOrchestrator` que coordina IOdometerCache + IRepository + RuleEngine. Los consumers Kafka NO usan MediatR — llaman directamente al orchestrator para maximizar throughput.

### Odómetro Redis — idempotencia por Kafka offset
La key de deduplicación `odometer-inc:{tenantId}:{vehicleId}:{kafkaOffset}` con TTL 48h previene doble conteo si el mismo mensaje es procesado dos veces. Degradación graceful: si Redis está caído, OdometerSnapshot.Unknown evita alertas falsas.

---

## Critical Environment Variables

- `KAFKA_CLUSTER_ID`: generate with `docker run --rm confluentinc/cp-kafka:7.7.1 kafka-storage random-uuid` — required for KRaft mode; changing it destroys the cluster
- `JWT_SIGNING_KEY`: minimum 64 chars — changing it invalidates all existing tokens
- `STRIPE_WEBHOOK_SECRET`: obtain from the Stripe dashboard when registering the webhook endpoint
- `POSTGRES_PASSWORD` / `TIMESCALE_PASSWORD`: minimum 32 chars
