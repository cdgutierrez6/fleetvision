## ADDED Requirements

### Requirement: Polling con FOR UPDATE SKIP LOCKED
El `*RelayWorker` SHALL hacer polling de la tabla `*_outbox_events` usando `SELECT ... FOR UPDATE SKIP LOCKED` para permitir múltiples instancias concurrentes sin colisiones.

#### Scenario: Worker obtiene filas pendientes sin bloquear otras instancias
- **WHEN** dos instancias del RelayWorker hacen polling simultáneamente
- **THEN** cada instancia obtiene un conjunto disjunto de filas; ninguna fila es procesada dos veces

#### Scenario: No hay filas pendientes
- **WHEN** la outbox está vacía o todas las filas tienen `processed_at IS NOT NULL`
- **THEN** el worker espera el intervalo de polling configurado y vuelve a intentar

---

### Requirement: Publicación idempotente a Kafka
El RelayWorker SHALL usar un producer Kafka con `EnableIdempotence = true` y `Acks = Acks.All` para garantizar que cada mensaje llega exactamente una vez al broker.

#### Scenario: Publicación exitosa
- **WHEN** el producer publica el mensaje y recibe ACK de todos los brokers del ISR
- **THEN** el RelayWorker actualiza `processed_at` en la fila de outbox

#### Scenario: Fallo transitorio del broker
- **WHEN** el producer no recibe ACK por timeout
- **THEN** el producer reintenta automáticamente (configuración interna Confluent); el RelayWorker no marca la fila como procesada hasta recibir ACK

---

### Requirement: Patrón de consumer group ID
Todos los consumers Kafka en FleetVision SHALL usar el patrón `<service-name>-service` como consumer group ID.

#### Scenario: Consumer registrado con ID correcto
- **WHEN** el servicio `geofencing` consume `telemetry.raw`
- **THEN** el consumer group ID es `geofencing-service`

---

### Requirement: Commit manual de offset
Los consumers Kafka SHALL usar `EnableAutoCommit = false` y hacer commit explícito del offset solo después del procesamiento exitoso del mensaje.

#### Scenario: Procesamiento exitoso
- **WHEN** el consumer procesa exitosamente un mensaje
- **THEN** hace commit del offset de ese mensaje antes de continuar con el siguiente

#### Scenario: Fallo durante procesamiento
- **WHEN** el consumer lanza una excepción durante el procesamiento
- **THEN** no hace commit del offset; el mensaje es reprocesado en el siguiente polling cycle

---

### Requirement: RelayWorker como BackgroundService
Cada servicio publisher SHALL registrar su `*RelayWorker` como `IHostedService` en `Program.cs` usando `AddHostedService<TelemetryRelayWorker>()`.

#### Scenario: Worker arranca con el host
- **WHEN** el servicio .NET arranca
- **THEN** el RelayWorker inicia su loop de polling sin intervención manual
