## 1. Schema de Outbox

- [ ] 1.1 Crear migration EF Core `AddTelemetryOutboxEvents` en `services/telemetry/` con tabla `telemetry_outbox_events` (id UUID PK, topic VARCHAR, payload JSONB, created_at TIMESTAMPTZ, processed_at TIMESTAMPTZ nullable)
- [ ] 1.2 Crear migration EF Core `AddGeofencingOutboxEvents` en `services/geofencing/` con tabla `geofencing_outbox_events` con el mismo schema
- [ ] 1.3 Registrar ambas migraciones en los `DbContext` correspondientes y verificar que son idempotentes (`CREATE TABLE IF NOT EXISTS`)

## 2. Interfaces de Publisher (Application Layer)

- [ ] 2.1 Definir interfaz `IVehiclePositionPublisher` en `FleetVision.Telemetry.Application` con método `PublishAsync(VehiclePosition position, CancellationToken ct)`
- [ ] 2.2 Definir interfaz `IViolationPublisher` en `FleetVision.Geofencing.Application` con método `PublishAsync(GeofenceViolation violation, CancellationToken ct)`
- [ ] 2.3 Actualizar `IngestTelemetryCommandHandler` para inyectar y usar `IVehiclePositionPublisher` en lugar de cualquier llamada directa a Kafka

## 3. Implementación de Outbox Publisher

- [ ] 3.1 Implementar `TelemetryOutboxPublisher : IVehiclePositionPublisher` en `FleetVision.Telemetry.Infrastructure` — escribe en `telemetry_outbox_events` usando el `DbContext` activo (misma transacción)
- [ ] 3.2 Implementar `GeofencingOutboxPublisher : IViolationPublisher` en `FleetVision.Geofencing.Infrastructure` — escribe en `geofencing_outbox_events`
- [ ] 3.3 Registrar ambas implementaciones en `DependencyInjection.cs` de cada servicio

## 4. Kafka Relay Workers

- [ ] 4.1 Implementar `TelemetryRelayWorker : BackgroundService` en `FleetVision.Telemetry.Infrastructure` — polling con `SELECT ... FOR UPDATE SKIP LOCKED` vía `NpgsqlDataSource`, producer idempotente (`EnableIdempotence=true`, `Acks.All`), topic `telemetry.raw` (12 particiones), actualiza `processed_at` al confirmar ACK
- [ ] 4.2 Implementar `GeofencingRelayWorker : BackgroundService` análogo para `geofencing_outbox_events` → topic `geofencing.violations`
- [ ] 4.3 Registrar ambos workers con `AddHostedService<T>()` en `Program.cs` de cada servicio
- [ ] 4.4 Exponer `RELAY_WORKER_ENABLED` como env var con default `true`; el worker evalúa este flag al iniciar para permitir rollback sin redeploy

## 5. DLQ y Manejo de Errores en Consumers

- [ ] 5.1 Crear topics DLQ en la configuración de infra: `telemetry.raw.dlq` y `geofencing.violations.dlq`
- [ ] 5.2 Actualizar el consumer de `geofencing` (`TelemetryKafkaConsumer`) para capturar excepciones de deserialización → publicar bytes raw al `telemetry.raw.dlq` con header `X-Error-Reason` → commit offset
- [ ] 5.3 Actualizar el consumer de `notifications` (`ViolationKafkaConsumer`) con la misma lógica de DLQ para `geofencing.violations.dlq`
- [ ] 5.4 Verificar que `EnableAutoCommit = false` está configurado en todos los consumers y que el commit es explícito post-procesamiento

## 6. Tests

- [ ] 6.1 Test unitario: `TelemetryOutboxPublisher` escribe en la tabla dentro de la misma transacción (mock de `DbContext`)
- [ ] 6.2 Test unitario: rollback de transacción no persiste la fila de outbox
- [ ] 6.3 Test de integración: `TelemetryRelayWorker` publica mensaje y actualiza `processed_at` (Testcontainers Kafka + PostgreSQL)
- [ ] 6.4 Test de integración: mensaje con payload inválido llega al DLQ y el consumer continúa sin bloquearse
- [ ] 6.5 Test de integración: dos instancias de RelayWorker concurrentes no procesan la misma fila (verifica `FOR UPDATE SKIP LOCKED`)

## 7. Verificación en Staging

- [ ] 7.1 Aplicar migrations en el entorno staging y confirmar que las tablas de outbox existen
- [ ] 7.2 Publicar un ping de prueba vía gRPC y verificar que aparece en `telemetry.raw` con `kafka-console-consumer --bootstrap-server localhost:9092 --topic telemetry.raw --from-beginning`
- [ ] 7.3 Simular fallo de Kafka (detener broker) y verificar que las filas quedan en outbox pending; al restaurar el broker, el RelayWorker las procesa automáticamente
