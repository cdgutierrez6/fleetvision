# FleetVision — CLAUDE.md
# Decisiones técnicas, lecciones aprendidas y contexto permanente

## Stack

| Capa | Tecnología | Versión | Nota |
|------|-----------|---------|------|
| Backend | .NET 8 LTS + Clean Architecture + DDD | 8.0.x | LTS hasta Nov 2026 |
| API Gateway | YARP | 2.x | Reemplaza Ocelot (legacy) |
| Identidad | OpenIddict | 5.x | IdentityServer4 está deprecado — NO usar |
| Mensajería | Apache Kafka KRaft | 3.8 (via Confluent 7.7.1) | Sin ZooKeeper — usar siempre KRaft |
| Schema Registry | Confluent Schema Registry | 7.7.1 | Protobuf obligatorio — sin esto los cambios de DTO rompen consumers |
| BD Telemetría | TimescaleDB 2.x + PostGIS | pg16 | Hypertable por semanas — PostgreSQL plano colapsa a escala |
| BD por servicio | PostgreSQL 16 + PostGIS 3.5 | 16.x | Database-per-Service — cada servicio tiene su propia DB |
| Cache | Redis 7 (AOF activado) | 7.x | AOF everysec — crítico para odómetros en maintenance service |
| Frontend | Angular 21 + Nx + Native Federation | 21.x | Signal Forms, signals, OnPush por defecto |
| Node | 22 LTS | 22.22.x | Gestionado con fnm |

## Decisiones Arquitectónicas

### Por qué TimescaleDB y no PostgreSQL plano
PostgreSQL sin extensión de series temporales colapsa cuando se insertan millones de pings porque:
- No hay particionamiento automático por tiempo
- Los índices B-Tree en timestamps degradan con el volumen
- Las vistas materializadas no se refrescan eficientemente

TimescaleDB agrega hypertables (particionamiento automático por tiempo), compresión nativa, y funciones de ventana temporal optimizadas.

### Por qué KRaft y no ZooKeeper
Apache Kafka 3.8 declaró ZooKeeper obsoleto. KRaft es el modo estándar desde 2024. Usar ZooKeeper en un proyecto nuevo es deuda técnica desde el día uno.

### Por qué OpenIddict y no IdentityServer4/Duende
- IdentityServer4: sin soporte desde Jan 2023
- Duende IdentityServer: licencia comercial obligatoria para producción
- OpenIddict: MIT license, mantenido activamente, integración nativa con ASP.NET Core

### Por qué YARP y no Ocelot
Ocelot no ha tenido releases significativos desde 2022. YARP (Yet Another Reverse Proxy) es el gateway recomendado por Microsoft, con soporte activo y mejor integración con .NET 8.

### Multi-tenancy: RLS en PostgreSQL
Row-Level Security se activa en cada tabla que contiene `tenant_id`. El claim del JWT se propaga vía `SET app.tenant_id = '<uuid>'` al inicio de cada transacción. Un tenant nunca ve datos de otro incluso si hay un bug a nivel de aplicación.

## Estructura del Monorepo

```
fleetvision/
├── services/          # Microservicios .NET 8 (Clean Architecture)
├── frontend/          # Nx workspace Angular 21 (Native Federation)
├── infra/             # Docker configs, IaC Bicep, Helm, scripts
├── proto/             # Definiciones Protobuf compartidas
└── .github/workflows/ # Pipelines CI/CD (1 por servicio)
```

## Convenciones de Código

### .NET 8 / C#
- Cada servicio: Domain / Application / Infrastructure / API
- No usar `SERIAL` — siempre `UUID` con `DEFAULT uuid_generate_v4()`
- Nunca concatenar SQL — siempre parámetros o EF Core
- `try/catch` explícito en todos los métodos `async` que llaman a externos
- Errores hacia el cliente: mensajes genéricos (no `ex.Message` crudo)
- OTel: instrumentar desde el día uno con `AddOpenTelemetry()`

### Angular 21
- `changeDetection: ChangeDetectionStrategy.OnPush` en todos los componentes
- Signal Forms para todos los formularios nuevos (no ReactiveFormsModule tradicional)
- Control flow nativo: `@if`, `@for`, `@defer` — no `*ngIf`, `*ngFor`
- NgRx Signal Store para estado complejo

### Kafka
- Outbox pattern para TODOS los publishers (no fire-and-forget)
- Consumers siempre idempotentes (verificar con `WHERE NOT EXISTS`)
- DLQ configurada para cada consumer group
- Schema Registry obligatorio antes del primer publish

## Puertos Dev (docker-compose.dev.yml)

| Servicio | Puerto host |
|---------|------------|
| PostgreSQL | 5434 (5432 ocupado por ai-workflow-hub) |
| TimescaleDB | 5435 (5433 ocupado por sap-triage-db) |
| Kafka broker 1 | 9092 |
| Kafka broker 2 | 9093 |
| Kafka broker 3 | 9094 |
| Schema Registry | 8081 |
| Redis | 6380 (6379 ocupado por ai-workflow-hub) |
| Jaeger UI | 16686 |
| OTLP gRPC | 4317 |
| OTLP HTTP | 4318 |
| Prometheus | 9090 |
| Grafana | 3000 |
| Loki | 3100 |

## Comandos útiles

```bash
# Levantar infraestructura
docker compose -f docker-compose.dev.yml up -d

# Ver estado de todos los contenedores
docker compose -f docker-compose.dev.yml ps

# Ver logs de Kafka
docker logs fv-kafka-1 --tail 50 -f

# Verificar topics
docker exec fv-kafka-1 kafka-topics --bootstrap-server localhost:9092 --list

# Verificar Schema Registry
curl http://localhost:8081/subjects

# Conectar a PostgreSQL
docker exec -it fv-postgres psql -U fv_admin -d identity_db

# Conectar a TimescaleDB
docker exec -it fv-timescaledb psql -U telemetry_user -d telemetry_db

# Verificar extensiones TimescaleDB
docker exec -it fv-timescaledb psql -U telemetry_user -d telemetry_db -c "\dx"

# Redis check
docker exec fv-redis redis-cli -a $REDIS_PASSWORD PING

# Parar todo (preserva volúmenes)
docker compose -f docker-compose.dev.yml down

# Parar todo y eliminar volúmenes (reset completo)
docker compose -f docker-compose.dev.yml down -v
```

## Lecciones Aprendidas

<!-- Se completa durante el desarrollo -->

## Variables de Entorno Críticas

- `KAFKA_CLUSTER_ID`: generar con `docker run --rm confluentinc/cp-kafka:7.7.1 kafka-storage random-uuid`
- `POSTGRES_PASSWORD`: mínimo 32 chars aleatorios
- `JWT_SIGNING_KEY`: mínimo 64 chars — si cambia, todos los tokens existentes se invalidan
- `STRIPE_WEBHOOK_SECRET`: obtener del dashboard de Stripe al registrar el webhook endpoint
