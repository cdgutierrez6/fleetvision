## ADDED Requirements

### Requirement: Transactional outbox write
El servicio publisher SHALL escribir el evento de dominio en la tabla `*_outbox_events` dentro de la misma transacción EF Core que el cambio de estado de dominio. Si la transacción hace rollback, el evento no debe persistir.

#### Scenario: Evento escrito junto al cambio de dominio
- **WHEN** el handler de dominio completa exitosamente y hace commit de la transacción EF Core
- **THEN** la fila en `*_outbox_events` queda persistida con `processed_at = NULL` y el payload serializado en JSON

#### Scenario: Rollback de la transacción no persiste el evento
- **WHEN** la transacción EF Core hace rollback por cualquier razón
- **THEN** no existe ninguna fila en `*_outbox_events` correspondiente a ese evento

---

### Requirement: Outbox schema
La tabla `*_outbox_events` SHALL tener las columnas: `id` (UUID, PK), `topic` (VARCHAR), `payload` (JSONB), `created_at` (TIMESTAMPTZ, default NOW()), `processed_at` (TIMESTAMPTZ, nullable).

#### Scenario: Fila creada con processed_at nulo
- **WHEN** se inserta un nuevo evento en la outbox
- **THEN** `processed_at` es NULL y `created_at` refleja el timestamp del insert

#### Scenario: Fila marcada como procesada
- **WHEN** el RelayWorker publica exitosamente el evento a Kafka
- **THEN** el RelayWorker actualiza `processed_at` con el timestamp actual en la misma conexión

---

### Requirement: IVehiclePositionPublisher interface
El Application layer SHALL depender de la interfaz `IVehiclePositionPublisher` (en el servicio `telemetry`) y nunca de Kafka directamente. La implementación de outbox implementa esta interfaz.

#### Scenario: Handler usa la interfaz, no Kafka
- **WHEN** el handler `IngestTelemetryCommandHandler` publica una posición
- **THEN** llama a `IVehiclePositionPublisher.PublishAsync(position)` sin conocimiento de Kafka

#### Scenario: La implementación de outbox escribe en DB
- **WHEN** se llama a `PublishAsync`
- **THEN** se inserta en `telemetry_outbox_events` con topic = `telemetry.raw` y el payload serializado
