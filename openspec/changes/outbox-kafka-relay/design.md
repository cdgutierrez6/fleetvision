## Context

FleetVision procesa flujos continuos de telemetría GPS y eventos de geocerca de flotas multi-tenant. Los servicios `telemetry` y `geofencing` necesitan publicar eventos a Kafka para que downstream consumers (predicciones de mantenimiento, notificaciones SignalR) los procesen. El riesgo original: un commit a DB exitoso seguido de un fallo de red antes de publicar a Kafka produce pérdida silenciosa de eventos — inaceptable en un sistema de flotas donde cada violación de geocerca puede disparar alertas de seguridad.

**Estado actual al diseñar**: Telemetry ingesta pings vía gRPC y escribe a TimescaleDB. Geofencing evalúa posiciones con PostGIS. Ninguno tenía aún un mecanismo de publicación transaccional.

## Goals / Non-Goals

**Goals:**
- Garantizar que cada evento de dominio se publique a Kafka exactamente una vez, incluso ante fallos parciales
- Desacoplar el Application layer de Kafka — ningún handler conoce el broker directamente
- Proveer visibilidad de mensajes fallidos mediante DLQ por topic
- Establecer el patrón base para todos los servicios publisher futuros

**Non-Goals:**
- Reemplazar gRPC para la ingesta de telemetría (sigue siendo la puerta de entrada)
- Implementar replay automático desde DLQ (es operacional, no parte de este cambio)
- Garantizar orden estricto entre particiones (el ordering es por `vehicle_id` dentro de cada partición)

## Decisions

### D1 — Outbox en DB del servicio vs. bus externo

**Decisión**: tabla `*_outbox_events` en la misma DB del servicio, escrita en la misma transacción EF Core.

**Alternativas consideradas**:
- _Kafka Transactions (EOS)_: requiere que el productor y el consumidor compartan el mismo cluster; introduce complejidad de coordinación y latencia de transacción distribuida.
- _Change Data Capture (Debezium)_: dependencia de infraestructura adicional (Kafka Connect), latencia de captura de WAL, complejidad operacional alta para F0.

**Rationale**: la outbox en la misma DB del servicio es la única opción que garantiza atomicidad sin coordinación distribuida. El RelayWorker la lee de forma asíncrona → latencia de entrega máxima = intervalo de polling (configurable).

---

### D2 — Polling con FOR UPDATE SKIP LOCKED vs. triggers / NOTIFY

**Decisión**: polling periódico con `SELECT FOR UPDATE SKIP LOCKED` usando `NpgsqlDataSource` raw (no EF Core).

**Alternativas consideradas**:
- _PostgreSQL LISTEN/NOTIFY_: elimina polling pero requiere conexión persistente por servicio; complejo de escalar horizontalmente.
- _EF Core DbContext_: introduce overhead de tracking y mapeo innecesario para una operación de lectura-delete bulk.

**Rationale**: `SKIP LOCKED` permite múltiples instancias del RelayWorker en paralelo sin colisiones. Raw SQL via `NpgsqlDataSource` minimiza overhead en el hot path de polling.

---

### D3 — Producer idempotente con Acks.All

**Decisión**: `EnableIdempotence = true`, `Acks = Acks.All`, `MaxInFlight = 5`.

**Rationale**: en caso de retry del RelayWorker (fallo antes de marcar como procesado), el producer idempotente evita duplicados en Kafka. `Acks.All` garantiza que el mensaje fue replicado a todos los brokers del ISR antes de confirmar.

---

### D4 — DLQ por topic, no global

**Decisión**: cada topic tiene su propio DLQ (`<topic>.dlq`).

**Rationale**: aísla el ruido entre dominios. Una explosión de errores en `telemetry.raw.dlq` no contamina la visibilidad de `geofencing.violations.dlq`. Facilita replay selectivo por dominio.

---

### D5 — Commit manual de offset en consumers

**Decisión**: `EnableAutoCommit = false`; commit explícito después del procesamiento exitoso.

**Rationale**: con auto-commit, un crash entre recibir el mensaje y procesarlo marca el offset como leído → pérdida. Con commit manual, el peor caso es reprocesamiento (at-least-once), que los consumers deben manejar con idempotencia.

## Risks / Trade-offs

| Riesgo | Mitigación |
|--------|-----------|
| La outbox crece si el RelayWorker se detiene mucho tiempo | TTL de limpieza de filas procesadas (`processed_at IS NOT NULL AND processed_at < NOW() - INTERVAL '48h'`) vía job periódico |
| Doble publicación si el RelayWorker falla justo después de publicar pero antes de marcar como procesado | Producer idempotente (D3) elimina duplicados en Kafka; consumers downstream deben ser idempotentes por diseño |
| Latencia adicional por polling interval | Configurable; default 500ms es aceptable para geocercas; telemetría ya tiene latencia de red inherente |
| SKIP LOCKED no es FIFO estricto | Aceptable — el orden garantizado es dentro de la misma partición Kafka por `vehicle_id`, no entre mensajes de distintos vehículos |

## Migration Plan

1. Aplicar migration EF Core que agrega `*_outbox_events` a cada servicio publisher (idempotente — `CREATE TABLE IF NOT EXISTS`)
2. Registrar `IVehiclePositionPublisher` / `IViolationPublisher` en DI apuntando a la implementación de outbox
3. Activar `*RelayWorker` como `BackgroundService` en `Program.cs`
4. Verificar en staging: publicar un ping y confirmar que aparece en `telemetry.raw` vía `kafka-console-consumer`
5. Deploy a producción — sin downtime, el worker arranca junto con el servicio

**Rollback**: deshabilitar el `RelayWorker` via feature flag de env var `RELAY_WORKER_ENABLED=false`; los eventos quedan en outbox pendientes y se procesan al re-habilitar.

## Open Questions

- ¿Frecuencia de polling óptima por entorno? (propuesta: 500ms dev, 200ms prod para telemetría)
- ¿Retención de filas procesadas en outbox: 48h o 7d? Depende de política de replay operacional
