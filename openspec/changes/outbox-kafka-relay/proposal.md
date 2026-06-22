## Why

Los servicios de FleetVision necesitan publicar eventos de dominio a Kafka de forma confiable sin acoplar la lógica de negocio al broker. Sin un mecanismo transaccional, una falla entre el commit de DB y la publicación a Kafka produce pérdida silenciosa de eventos — crítico en telemetría de flotas donde cada GPS ping y cada violación de geocerca debe procesarse exactamente una vez.

## What Changes

- Cada servicio publisher expone una tabla `*_outbox_events` en su propia DB, escrita en la misma transacción EF Core que el cambio de dominio
- Un `*RelayWorker` (`BackgroundService`) hace polling de la tabla con `FOR UPDATE SKIP LOCKED` y publica a Kafka usando producer idempotente (`Acks.All`)
- Mensajes que fallan tras reintentos van al topic `<topic>.dlq` para inspección y replay
- Consumers usan commit de offset manual post-procesamiento exitoso (no `EnableAutoCommit`)
- Consumer group IDs siguen el patrón `<service-name>-service`
- Servicios implementados: `telemetry` (topic `telemetry.raw`, 12 particiones) y `geofencing` (topic `geofencing.violations`)

## Capabilities

### New Capabilities

- `outbox-event-store`: Mecanismo de almacenamiento transaccional de eventos de dominio en tabla de outbox, garantizando atomicidad con el cambio de estado
- `kafka-relay-worker`: Worker de polling que drena la outbox y publica eventos a Kafka con garantías de entrega (idempotencia, Acks.All)
- `dlq-error-handling`: Manejo de mensajes fallidos mediante Dead Letter Queue por topic, con visibilidad para replay y diagnóstico

### Modified Capabilities

_(ninguna — esta es la implementación inicial del bus de eventos)_

## Impact

- **Servicios afectados**: `services/telemetry/` y `services/geofencing/` (publishers actuales); cualquier servicio futuro que publique eventos hereda este patrón
- **Schema de DB**: cada servicio publisher agrega tabla `*_outbox_events` con columnas `id`, `topic`, `payload`, `created_at`, `processed_at`
- **Kafka**: topics `telemetry.raw` (12 particiones), `geofencing.violations`, y sus respectivos DLQs (`telemetry.raw.dlq`, `geofencing.violations.dlq`)
- **Consumers afectados**: `geofencing` consume `telemetry.raw`; `notifications` consume `geofencing.violations` — ambos con commit manual
- **Dependencias**: `Confluent.Kafka` NuGet, `NpgsqlDataSource` para el polling de outbox vía raw SQL
