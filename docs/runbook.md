# FleetVision — Runbook Operativo

## Diagnóstico Rápido

### ¿El sistema está caído?

```bash
# 1. Verificar gateway (punto de entrada)
curl https://<GATEWAY_URL>/health

# 2. Verificar servicios individuales (dev local)
curl http://localhost:5001/health  # identity
curl http://localhost:5002/health  # tenant-management
curl http://localhost:5003/health  # billing
curl http://localhost:5004/health  # fleet-assets
curl http://localhost:5005/health  # telemetry
curl http://localhost:5006/health  # geofencing
curl http://localhost:5007/health  # predictive-maintenance
curl http://localhost:5008/health  # reporting
curl http://localhost:5009/health  # notifications

# 3. Verificar infra
docker compose -f docker-compose.dev.yml ps
```

---

## Incidentes Comunes

### INC-001 — Kafka consumer lag acumulado (alertas con retraso)

**Síntoma:** Alertas de geofencing/mantenimiento llegan > 5s después del ping GPS.

**Diagnóstico:**
```bash
# Ver lag del consumer group de geofencing
docker exec fv-kafka-1 kafka-consumer-groups \
  --bootstrap-server localhost:9092 \
  --group geofencing-service \
  --describe

# Ver lag del consumer group de predictive-maintenance
docker exec fv-kafka-1 kafka-consumer-groups \
  --bootstrap-server localhost:9092 \
  --group predictive-maintenance-service \
  --describe
```

**Causa típica:** OOM en el pod del consumer → restart → re-proceso desde el último offset commiteado.

**Fix:**
```bash
# Escalar replicas (Azure Container Apps)
az containerapp update --name fv-geofencing-prod --resource-group fleetvision-prod \
  --min-replicas 2 --max-replicas 10

# O en Docker Compose (dev)
docker compose -f docker-compose.dev.yml up --scale geofencing=3 -d
```

---

### INC-002 — DLQ con mensajes acumulados (telemetry.raw.dlq)

**Síntoma:** Consumer falla con deserialization errors; mensajes van a DLQ.

**Diagnóstico:**
```bash
# Ver cuántos mensajes hay en el DLQ
docker exec fv-kafka-1 kafka-consumer-groups \
  --bootstrap-server localhost:9092 \
  --group dlq-inspector \
  --describe

# Leer mensajes del DLQ (solo primeros 10)
docker exec fv-kafka-1 kafka-console-consumer \
  --bootstrap-server localhost:9092 \
  --topic telemetry.raw.dlq \
  --from-beginning \
  --max-messages 10 \
  --property print.timestamp=true
```

**Fix:**
1. Identificar el dispositivo que envía mensajes inválidos por su `tenant_id` en el header.
2. Corregir el Protobuf en el dispositivo o añadir validación adicional en el consumer.
3. Si los mensajes son recuperables, republicarlos al topic original después de corregir el schema.

---

### INC-003 — Redis caído (odómetro no acumula)

**Síntoma:** OdometerSnapshot.Unknown → sin alertas de mantenimiento por km.

**Diagnóstico:**
```bash
# Dev
docker exec fv-redis redis-cli -a $REDIS_PASSWORD PING

# Azure
az redis show --name <redis-name> --resource-group fleetvision-prod --query "provisioningState"
```

**Fix:**
- Redis Basic C0 no tiene HA. Si cae en prod, upgrade a Standard C1.
- El sistema hace graceful degradation — no hay pérdida de datos, solo se detienen las alertas por km hasta que Redis vuelva.
- Tras recuperar Redis, los odómetros se recalculan automáticamente con los próximos pings.

---

### INC-004 — RLS bloqueando todos los queries (app.tenant_id no configurado)

**Síntoma:** Todos los endpoints de datos devuelven 500 o resultados vacíos para un tenant específico.

**Diagnóstico:**
```bash
# Verificar que el interceptor RLS está registrado
grep -r "TenantRlsInterceptor" services/<servicio>/src/ --include="*.cs"

# Verificar en PostgreSQL
docker exec -it fv-postgres psql -U fv_admin -d fleet_db -c \
  "SELECT current_setting('app.tenant_id', true);"
```

**Fix:**
- Verificar que `TenantContextMiddleware` está registrado en `Program.cs` y llama a `tenantContext.SetTenantId()`.
- Verificar que el JWT incluye el claim `tenant_id` — puede estar ausente si se emitió con un token de sistema.

---

### INC-005 — Stripe webhook falla (403 o 400)

**Síntoma:** Subscripciones no se actualizan tras pago en Stripe.

**Diagnóstico:**
```bash
# Ver logs del billing service
docker logs fv-billing 2>&1 | grep -i "stripe\|webhook" | tail -50

# Verificar que el endpoint /api/billing/webhooks está marcado como anonymous en YARP
grep -A5 "webhook" services/gateway/src/FleetVision.Gateway/appsettings.json
```

**Fix más común:** El `STRIPE_WEBHOOK_SECRET` no coincide con el configurado en el dashboard de Stripe. Regenerar en Stripe Dashboard y actualizar en Key Vault / .env.

---

## Backups

### TimescaleDB (telemetría)

```bash
# Backup diario completo
docker exec fv-timescaledb pg_dump \
  -U telemetry_user \
  -Fc \
  --no-password \
  telemetry_db > "backups/telemetry_$(date +%Y%m%d_%H%M%S).dump"

# Backup incremental (solo WAL — requiere configurar archive_mode en postgres.conf)
# Para producción, usar Azure Database for PostgreSQL built-in backups

# Restore desde dump
docker exec -i fv-timescaledb pg_restore \
  -U telemetry_user \
  -d telemetry_db \
  --no-password \
  --clean \
  < backups/telemetry_20260601_000000.dump
```

### PostgreSQL (datos de negocio)

```bash
# Dump de todas las DBs de negocio
for DB in identity_db tenant_db billing_db fleet_db geofencing_db notifications_db; do
  docker exec fv-postgres pg_dump \
    -U fv_admin \
    -Fc \
    $DB > "backups/${DB}_$(date +%Y%m%d_%H%M%S).dump"
done
```

### Verificar restore (mensual)

```bash
# Crear DB temporal
docker exec fv-postgres createdb -U fv_admin test_restore_fleet_db

# Restore
docker exec -i fv-postgres pg_restore -U fv_admin -d test_restore_fleet_db < backups/fleet_db_latest.dump

# Verificar integridad
docker exec fv-postgres psql -U fv_admin -d test_restore_fleet_db -c \
  "SELECT COUNT(*) FROM vehicles; SELECT COUNT(*) FROM fleets;"

# Limpiar
docker exec fv-postgres dropdb -U fv_admin test_restore_fleet_db
```

---

## Escalado

### Horizontal (más replicas)

```bash
# Azure Container Apps
az containerapp update \
  --name fv-telemetry-prod \
  --resource-group fleetvision-prod \
  --min-replicas 3 \
  --max-replicas 20
```

### Kafka partitions (más throughput)

```bash
# NOTA: Solo se pueden AUMENTAR partitions, nunca disminuir
docker exec fv-kafka-1 kafka-topics \
  --bootstrap-server localhost:9092 \
  --alter \
  --topic telemetry.raw \
  --partitions 24
```

---

## Métricas Clave (Grafana)

| Panel | Query | Alerta si |
|-------|-------|-----------|
| Telemetry ingesta rate | `rate(grpc_server_handled_total{service="telemetry"}[1m])` | < 1000/s en horario pico |
| Consumer lag geofencing | Kafka exporter metric | > 10,000 mensajes |
| p99 latency gateway | `histogram_quantile(0.99, rate(http_request_duration_seconds_bucket{service="gateway"}[5m]))` | > 2s |
| Redis memory usage | `redis_memory_used_bytes` | > 80% de maxmemory |
| DLQ count | `kafka_topic_partition_current_offset{topic=~".*\\.dlq"}` | > 0 (cualquier DLQ con mensajes) |

---

## Contactos

| Rol | Contacto | Disponibilidad |
|-----|---------|---------------|
| Tech Lead | Cristian Gutierrez | Horario laboral |
| On-call | efiziai.notificaciones@gmail.com | 24/7 para P0/P1 |
