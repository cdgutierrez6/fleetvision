import grpc from 'k6/net/grpc';
import { check, sleep } from 'k6';
import { Counter, Rate, Trend } from 'k6/metrics';

// Custom metrics
const successRate     = new Rate('telemetry_success_rate');
const errorCount      = new Counter('telemetry_errors');
const ingestLatency   = new Trend('telemetry_ingest_latency_ms');

// Target: 10,000 pings/sec sustained for 5 minutes
// k6 VUs × iterations/sec = target RPS
// 500 VUs × 20 req/s = 10,000 req/s
export const options = {
  scenarios: {
    ramp_up: {
      executor: 'ramping-arrival-rate',
      startRate: 100,
      timeUnit: '1s',
      preAllocatedVUs: 200,
      maxVUs: 1000,
      stages: [
        { target: 1000,  duration: '30s' },  // ramp to 1k/s
        { target: 5000,  duration: '60s' },  // ramp to 5k/s
        { target: 10000, duration: '60s' },  // ramp to 10k/s
        { target: 10000, duration: '300s' }, // sustain 10k/s for 5 minutes
        { target: 0,     duration: '30s' },  // ramp down
      ],
    },
  },
  thresholds: {
    'telemetry_success_rate': ['rate>0.99'],         // 99% success required
    'telemetry_ingest_latency_ms': ['p95<500'],      // p95 latency < 500ms
    'telemetry_ingest_latency_ms': ['p99<2000'],     // p99 latency < 2s
    'grpc_req_duration':            ['p95<500'],
  },
};

const client = new grpc.Client();
client.load(['../../proto'], 'telemetry.proto');

const GATEWAY_GRPC = __ENV.GATEWAY_GRPC_URL || 'localhost:5005';

// Pre-generate tenant and vehicle IDs to simulate multi-tenant traffic
const TENANTS  = Array.from({ length: 10 }, () => crypto.randomUUID());
const VEHICLES = Array.from({ length: 100 }, () => crypto.randomUUID());

function randomElement(arr) {
  return arr[Math.floor(Math.random() * arr.length)];
}

function randomFloat(min, max) {
  return Math.random() * (max - min) + min;
}

export default function () {
  const tenantId  = randomElement(TENANTS);
  const vehicleId = randomElement(VEHICLES);

  // Simulate a vehicle GPS ping with OBD2 data
  const payload = {
    tenant_id:           tenantId,
    vehicle_id:          vehicleId,
    latitude:            randomFloat(-90,  90),
    longitude:           randomFloat(-180, 180),
    speed_kmh:           randomFloat(0, 120),
    distance_km:         randomFloat(0.1, 2.5),
    timestamp_unix_ms:   Date.now(),
    obd2_code:           Math.random() < 0.01 ? 'P0300' : '',  // 1% chance of OBD2 fault
  };

  const start = Date.now();

  // gRPC call to telemetry service
  try {
    client.connect(GATEWAY_GRPC, { plaintext: true, timeout: '5s' });

    const res = client.invoke('fleetvision.telemetry.v1.TelemetryService/IngestTelemetry', payload, {
      metadata: {
        'Authorization': `Bearer ${__ENV.TEST_JWT_TOKEN || 'test-token'}`,
        'X-Tenant-Id': tenantId,
      },
      timeout: '5s',
    });

    const latency = Date.now() - start;
    ingestLatency.add(latency);

    const ok = check(res, {
      'status is OK': (r) => r && r.status === grpc.StatusOK,
    });

    successRate.add(ok);
    if (!ok) errorCount.add(1);

  } catch (e) {
    errorCount.add(1);
    successRate.add(false);
    ingestLatency.add(Date.now() - start);
  } finally {
    client.close();
  }
}

export function handleSummary(data) {
  return {
    'k6-results/telemetry-load-test-summary.json': JSON.stringify(data, null, 2),
    stdout: textSummary(data, { indent: ' ', enableColors: true }),
  };
}

function textSummary(data, opts = {}) {
  const metrics = data.metrics;
  const indent  = opts.indent || '';
  const lines   = [
    `${indent}=== FleetVision Telemetry Load Test — Results ===`,
    `${indent}Success rate:  ${(metrics.telemetry_success_rate?.values?.rate * 100 || 0).toFixed(2)}%`,
    `${indent}Ingest p95:    ${metrics.telemetry_ingest_latency_ms?.values?.p95?.toFixed(0) || '-'} ms`,
    `${indent}Ingest p99:    ${metrics.telemetry_ingest_latency_ms?.values?.p99?.toFixed(0) || '-'} ms`,
    `${indent}Total errors:  ${metrics.telemetry_errors?.values?.count || 0}`,
    `${indent}Max RPS:       ${metrics.grpc_reqs?.values?.rate?.toFixed(0) || '-'} req/s`,
  ];
  return lines.join('\n');
}
