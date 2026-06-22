## ADDED Requirements

### Requirement: DLQ por topic
Cada topic Kafka publisher SHALL tener un Dead Letter Queue dedicado con nombre `<topic>.dlq` (ej: `telemetry.raw.dlq`, `geofencing.violations.dlq`).

#### Scenario: DLQ existe antes de que el servicio arranque
- **WHEN** el servicio arranca en un entorno nuevo
- **THEN** el topic `<topic>.dlq` existe en Kafka (creado vía configuración de infraestructura, no en runtime)

---

### Requirement: Envío al DLQ tras fallo de deserialización
El consumer SHALL enviar el mensaje raw (bytes) al DLQ correspondiente cuando la deserialización del payload falla, usando un producer idempotente con `Acks.All`.

#### Scenario: Mensaje inválido llega al consumer
- **WHEN** el consumer recibe un mensaje cuyo payload no puede deserializarse al tipo esperado
- **THEN** el mensaje raw se publica en `<topic>.dlq` y el offset es commiteado para no reprocesar

#### Scenario: Mensaje inválido no bloquea el procesamiento
- **WHEN** hay un mensaje inválido en la partición
- **THEN** el consumer lo envía al DLQ y continúa con los mensajes siguientes sin detenerse

---

### Requirement: Envío al DLQ tras fallo de procesamiento
El consumer SHALL enviar el mensaje al DLQ cuando el procesamiento de negocio falla tras agotar los reintentos internos.

#### Scenario: Fallo de negocio persistente
- **WHEN** el procesamiento del mensaje lanza excepción en todos los intentos configurados
- **THEN** el mensaje se publica en `<topic>.dlq` con metadata de error (header `X-Error-Reason`) y el offset es commiteado

#### Scenario: El DLQ no bloquea el procesamiento normal
- **WHEN** el producer del DLQ falla al enviar
- **THEN** se loguea el error con nivel CRITICAL y se continúa (el mensaje se perderá — aceptable frente a bloquear el consumer)
