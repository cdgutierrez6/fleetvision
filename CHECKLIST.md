# FleetVision — Checklist de Progreso

Estado general: **F0 en curso**

---

## F0 — Infraestructura Base

- [x] Estructura de monorepo creada
- [x] `.env.example` con todas las variables
- [x] `docker-compose.dev.yml` — Postgres, TimescaleDB, Kafka KRaft 3 nodos, Schema Registry, Redis, Jaeger, Prometheus, Grafana, Loki
- [x] Scripts de init de DB (`infra/db/init-postgres.sql`, `init-timescale.sql`)
- [x] Config Prometheus, Loki, Grafana datasources
- [x] `.gitignore`, `.nvmrc` (Node 22), `.node-version`
- [x] `README.md`, `CHECKLIST.md`, `CLAUDE.md`
- [x] Repo GitHub creado: `cdgutierrez6/fleetvision`
- [ ] `.env` local completado con contraseñas reales de dev
- [ ] `KAFKA_CLUSTER_ID` generado y añadido a `.env`
- [ ] `docker compose -f docker-compose.dev.yml up -d` levanta sin errores
- [ ] PostgreSQL health check verde — todas las DBs creadas
- [ ] TimescaleDB health check verde — extensiones activas
- [ ] Kafka 3 brokers health check verde
- [ ] Schema Registry accesible en `:8081`
- [ ] Redis health check verde — responde a PING
- [ ] Kafka topics creados (verificar con `kafka-topics --list`)
- [ ] Jaeger UI accesible en `http://localhost:16686`
- [ ] Grafana accesible en `http://localhost:3000`
- [ ] Prometheus scrape activo en `http://localhost:9090`
- [ ] Loki healthy en `http://localhost:3100/ready`
- [ ] Datasources Prometheus + Loki + Jaeger configurados en Grafana

---

## F1 — Identity & Access Service

- [ ] RFC-002: Dominio + OpenIddict + JWT + RBAC
- [ ] RFC-003: API Gateway YARP
- [ ] Tests unitarios > 80% coverage
- [ ] Health checks `/health` y `/ready`
- [ ] RLS verificado en DB

---

## F2 — Tenant Management + Billing

- [ ] RFC-004: Tenant Management + Planes
- [ ] RFC-005: Billing + Stripe webhooks
- [ ] Evento `tenant.provisioned` en Kafka
- [ ] Evento `billing.subscription.changed` en Kafka

---

## F3 — Fleet & Assets

- [ ] RFC-006: CRUD vehículos, flotas, conductores, geofences
- [ ] PostGIS índices GIST verificados
- [ ] RLS multi-tenant verificado

---

## F4 — Telemetry Ingestion

- [ ] RFC-007: gRPC + HTTP fallback + Redis cache
- [ ] RFC-008: Consumer → TimescaleDB hypertable
- [ ] Throughput: 1,000 pings/seg en dev (k6)

---

## F5 — Geofencing & Safety

- [ ] RFC-009: Consumer + ST_Contains + alertas
- [ ] Latencia alerta < 2 segundos desde ping

---

## F6 — Predictive Maintenance

- [ ] RFC-012: Consumer + odómetro Redis + OBD2 + reglas

---

## F7 — Reporting & Analytics

- [ ] RFC-011: CQRS + TimescaleDB views + PDF export

---

## F8 — Notifications

- [ ] RFC-010: Consumer + WebSocket + email

---

## F9 — Frontend Angular 21

- [ ] RFC-013: Shell + Nx + Native Federation
- [ ] RFC-014: MFE Fleet
- [ ] RFC-015: MFE Monitoring (mapa + WebSocket)
- [ ] MFE Reports, Alerts, Admin, Billing

---

## F10 — Observabilidad + Resiliencia

- [ ] RFC-016: OTel en todos los servicios
- [ ] RFC-017: Polly + Outbox + DLQ + health checks

---

## F11 — CI/CD + Azure

- [ ] RFC-018: GitHub Actions + Azure Bicep + Container Apps

---

## F12 — Hardening + Launch

- [ ] RFC-019: Security checklist + k6 load test + backups
- [ ] OWASP Top 10 verde
- [ ] k6: 10k pings/seg sostenidos 5 min
- [ ] Backup TimescaleDB restaurable verificado
