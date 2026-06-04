#!/bin/bash
# FleetVision — Kafka Topics Init Script
# Crea todos los topics del sistema con la configuración correcta de particiones y replicación.
# Se ejecuta una sola vez al inicializar el cluster Kafka.

set -e

BOOTSTRAP="kafka-1:19092"

echo "=== FleetVision — Creando Kafka Topics ==="

# Esperar a que Kafka esté listo (máximo 60 seg)
echo "Esperando Kafka..."
for i in $(seq 1 30); do
  kafka-broker-api-versions --bootstrap-server="$BOOTSTRAP" > /dev/null 2>&1 && break
  echo "  Intento $i/30 — esperando 2s..."
  sleep 2
done

# Telemetría (alta throughput — 12 particiones)
kafka-topics --bootstrap-server "$BOOTSTRAP" --create --if-not-exists --topic telemetry.raw --partitions 12 --replication-factor 3 --config retention.ms=604800000
kafka-topics --bootstrap-server "$BOOTSTRAP" --create --if-not-exists --topic telemetry.raw.dlq --partitions 3 --replication-factor 3

# Geofencing
kafka-topics --bootstrap-server "$BOOTSTRAP" --create --if-not-exists --topic geofence.violation --partitions 3 --replication-factor 3
kafka-topics --bootstrap-server "$BOOTSTRAP" --create --if-not-exists --topic geofence.violation.dlq --partitions 1 --replication-factor 3

# Seguridad vial
kafka-topics --bootstrap-server "$BOOTSTRAP" --create --if-not-exists --topic driver.behavior.alert --partitions 3 --replication-factor 3
kafka-topics --bootstrap-server "$BOOTSTRAP" --create --if-not-exists --topic driver.behavior.alert.dlq --partitions 1 --replication-factor 3

# Mantenimiento
kafka-topics --bootstrap-server "$BOOTSTRAP" --create --if-not-exists --topic maintenance.scheduled --partitions 3 --replication-factor 3
kafka-topics --bootstrap-server "$BOOTSTRAP" --create --if-not-exists --topic vehicle.alert --partitions 3 --replication-factor 3

# Dominio de flota
kafka-topics --bootstrap-server "$BOOTSTRAP" --create --if-not-exists --topic vehicle.created --partitions 3 --replication-factor 3
kafka-topics --bootstrap-server "$BOOTSTRAP" --create --if-not-exists --topic vehicle.updated --partitions 3 --replication-factor 3
kafka-topics --bootstrap-server "$BOOTSTRAP" --create --if-not-exists --topic geofence.created --partitions 1 --replication-factor 3

# SaaS (baja frecuencia)
kafka-topics --bootstrap-server "$BOOTSTRAP" --create --if-not-exists --topic tenant.provisioned --partitions 1 --replication-factor 3
kafka-topics --bootstrap-server "$BOOTSTRAP" --create --if-not-exists --topic billing.subscription.changed --partitions 1 --replication-factor 3

echo ""
echo "=== Topics creados exitosamente ==="
kafka-topics --bootstrap-server "$BOOTSTRAP" --list
