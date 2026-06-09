# FleetVision - Runtime Smoke Test (PowerShell 5.1 compatible)
# Ejecutar DESPUES de: docker compose -f docker-compose.dev.yml up -d
#
# Uso: .\infra\scripts\smoke-test.ps1

param(
    [string]$GatewayUrl   = "http://localhost:5000",
    [string]$IdentityUrl  = "http://localhost:5001",
    [int]   $TimeoutSec   = 10
)

$ErrorActionPreference = "Continue"
$script:PASS = 0
$script:FAIL = 0

function Test-Http {
    param([string]$Label, [string]$Url, [int[]]$ExpectedCodes = @(200))
    try {
        $resp = Invoke-WebRequest -Uri $Url -TimeoutSec $TimeoutSec -UseBasicParsing -ErrorAction Stop
        $code = $resp.StatusCode
        if ($ExpectedCodes -contains $code) {
            Write-Host "  [PASS] $Label ($code)" -ForegroundColor Green
            $script:PASS++
        } else {
            Write-Host "  [FAIL] $Label - expected $($ExpectedCodes -join '/'), got $code" -ForegroundColor Red
            $script:FAIL++
        }
    } catch {
        $msg = $_.Exception.Message
        # Invoke-WebRequest throws on 4xx/5xx - check the actual code
        if ($_.Exception.Response -ne $null) {
            $code = [int]$_.Exception.Response.StatusCode
            if ($ExpectedCodes -contains $code) {
                Write-Host "  [PASS] $Label ($code)" -ForegroundColor Green
                $script:PASS++
                return
            }
        }
        Write-Host "  [FAIL] $Label - $msg" -ForegroundColor Red
        $script:FAIL++
    }
}

function Test-Docker {
    param([string]$Label, [string]$Container, [string]$Command)
    try {
        $out = docker exec $Container sh -c $Command 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Host "  [PASS] $Label" -ForegroundColor Green
            $script:PASS++
        } else {
            Write-Host "  [FAIL] $Label - $out" -ForegroundColor Red
            $script:FAIL++
        }
    } catch {
        Write-Host "  [FAIL] $Label - $($_.Exception.Message)" -ForegroundColor Red
        $script:FAIL++
    }
}

# ---------------------------------------------------------------
Write-Host ""
Write-Host "=== FleetVision Smoke Test ===" -ForegroundColor Cyan
Write-Host "Gateway : $GatewayUrl"
Write-Host "Identity: $IdentityUrl"
Write-Host ""

# ---------------------------------------------------------------
Write-Host "-- FASE 1: Infraestructura --" -ForegroundColor Cyan

Test-Docker "PostgreSQL ready"   "fv-postgres"    "pg_isready -U fv_admin -d postgres"
Test-Docker "TimescaleDB ready"  "fv-timescaledb" "pg_isready -U telemetry_user -d telemetry_db"
Test-Docker "Redis PING"         "fv-redis"       "redis-cli --no-auth-warning -a FVdev2026!redis#secure PING"
Test-Http   "Schema Registry"    "http://localhost:8081/subjects"
Test-Http   "Prometheus healthy" "http://localhost:9090/-/healthy"
Test-Http   "Loki ready"         "http://localhost:3100/ready"
Test-Http   "Grafana login page" "http://localhost:3000/login"
Test-Http   "Jaeger UI"          "http://localhost:16686"
Test-Docker "Kafka topic exists" "fv-kafka-1" "kafka-topics --bootstrap-server localhost:9092 --list"

# ---------------------------------------------------------------
Write-Host ""
Write-Host "-- FASE 2: Health Checks servicios --" -ForegroundColor Cyan

Test-Http "Identity       /health" "$IdentityUrl/health"
Test-Http "TenantMgmt     /health" "http://localhost:5002/health"
Test-Http "Billing        /health" "http://localhost:5003/health"
Test-Http "Fleet Assets   /health" "http://localhost:5004/health"
Test-Http "Telemetry      /health" "http://localhost:5005/health"
Test-Http "Geofencing     /health" "http://localhost:5006/health"
Test-Http "Predictive     /health" "http://localhost:5007/health"
Test-Http "Reporting      /health" "http://localhost:5008/health"
Test-Http "Notifications  /health" "http://localhost:5009/health"
Test-Http "Gateway        /health" "$GatewayUrl/health"

# ---------------------------------------------------------------
Write-Host ""
Write-Host "-- FASE 3: Auth E2E flow --" -ForegroundColor Cyan

$timestamp = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
$email = "smoke.$timestamp@fleetvision.test"

# 3.1 Register (POST /auth/register — no /api prefix; body: RegisterCommand fields)
$companySlug = "smoketest$timestamp"
$regJson = "{""companyName"":""Smoke Test Co $timestamp"",""adminEmail"":""$email"",""adminPassword"":""SmokeTest1!2026"",""adminFirstName"":""Smoke"",""adminLastName"":""Tester""}"
try {
    $regResp = Invoke-WebRequest -Uri "$IdentityUrl/auth/register" `
        -Method POST -Body $regJson -ContentType "application/json" `
        -UseBasicParsing -TimeoutSec $TimeoutSec -ErrorAction Stop
    $regData = $regResp.Content | ConvertFrom-Json
    Write-Host "  [PASS] Register ($($regResp.StatusCode))" -ForegroundColor Green
    $script:PASS++
    # Use accessToken from register response to skip a separate login
    if ($regData.accessToken) { $jwt = $regData.accessToken }
} catch {
    if ($_.Exception.Response -ne $null) {
        $code = [int]$_.Exception.Response.StatusCode
        Write-Host "  [WARN] Register returned $code" -ForegroundColor Yellow
    } else {
        Write-Host "  [FAIL] Register - $($_.Exception.Message)" -ForegroundColor Red
        $script:FAIL++
    }
}

# 3.2 Login (POST /auth/login — only if register didn't give us a token)
$loginJson = "{""email"":""$email"",""password"":""SmokeTest1!2026""}"
if ($jwt -eq $null) {
    try {
        $loginResp = Invoke-WebRequest -Uri "$IdentityUrl/auth/login" `
            -Method POST -Body $loginJson -ContentType "application/json" `
            -UseBasicParsing -TimeoutSec $TimeoutSec -ErrorAction Stop
        $loginData = $loginResp.Content | ConvertFrom-Json
        if ($loginData.accessToken) { $jwt = $loginData.accessToken }
        if ($jwt) {
            Write-Host "  [PASS] Login - JWT obtenido ($($jwt.Length) chars)" -ForegroundColor Green
            $script:PASS++
        } else {
            Write-Host "  [FAIL] Login - respuesta sin accessToken: $($loginResp.Content)" -ForegroundColor Red
            $script:FAIL++
        }
    } catch {
        Write-Host "  [FAIL] Login - $($_.Exception.Message)" -ForegroundColor Red
        $script:FAIL++
    }
} else {
    Write-Host "  [PASS] Login - JWT ya obtenido del registro ($($jwt.Length) chars)" -ForegroundColor Green
    $script:PASS++
}

# 3.3 Gateway auth validation (200 o 403 = auth funciona; 401 = JWT rechazado)
if ($jwt -ne $null) {
    $headers = @{ Authorization = "Bearer $jwt" }
    try {
        $gwResp = Invoke-WebRequest -Uri "$GatewayUrl/api/tenants" `
            -Headers $headers -UseBasicParsing -TimeoutSec $TimeoutSec -ErrorAction Stop
        Write-Host "  [PASS] Gateway JWT routing ($($gwResp.StatusCode))" -ForegroundColor Green
        $script:PASS++
    } catch {
        if ($_.Exception.Response -ne $null) {
            $code = [int]$_.Exception.Response.StatusCode
            if ($code -in @(200, 403)) {
                Write-Host "  [PASS] Gateway JWT routing ($code - auth valido)" -ForegroundColor Green
                $script:PASS++
            } else {
                Write-Host "  [FAIL] Gateway JWT routing - HTTP $code" -ForegroundColor Red
                $script:FAIL++
            }
        } else {
            Write-Host "  [FAIL] Gateway JWT routing - $($_.Exception.Message)" -ForegroundColor Red
            $script:FAIL++
        }
    }
} else {
    Write-Host "  [SKIP] Gateway JWT routing - sin JWT del paso anterior" -ForegroundColor Yellow
}

# ---------------------------------------------------------------
Write-Host ""
Write-Host "-- FASE 4: Kafka Topics --" -ForegroundColor Cyan

$expectedTopics = @(
    "telemetry.raw",
    "telemetry.raw.dlq",
    "geofencing.violations",
    "geofencing.violations.dlq",
    "driver.behavior.alert",
    "driver.behavior.alert.dlq",
    "maintenance.scheduled",
    "maintenance.scheduled.dlq",
    "vehicle.alert",
    "vehicle.alert.dlq",
    "vehicle.created",
    "vehicle.updated",
    "geofence.created",
    "tenant.provisioned",
    "billing.subscription.changed"
)

try {
    $topicList = docker exec fv-kafka-1 kafka-topics --bootstrap-server localhost:9092 --list 2>&1
    foreach ($topic in $expectedTopics) {
        if ($topicList -match [regex]::Escape($topic)) {
            Write-Host "  [PASS] Topic: $topic" -ForegroundColor Green
            $script:PASS++
        } else {
            Write-Host "  [FAIL] Topic faltante: $topic" -ForegroundColor Red
            $script:FAIL++
        }
    }
} catch {
    Write-Host "  [FAIL] No se pudo listar Kafka topics - $($_.Exception.Message)" -ForegroundColor Red
    $script:FAIL++
}

# ---------------------------------------------------------------
Write-Host ""
Write-Host "======================================" -ForegroundColor Cyan
$total = $script:PASS + $script:FAIL
if ($script:FAIL -eq 0) {
    Write-Host "RESULTADO: $($script:PASS)/$total PASSED - INFRA OK" -ForegroundColor Green
    Write-Host ""
    Write-Host "Siguiente paso: k6 load test" -ForegroundColor Yellow
    Write-Host "  k6 run infra/k6/telemetry-load-test.js" -ForegroundColor Yellow
} else {
    Write-Host "RESULTADO: $($script:PASS)/$total PASSED - $($script:FAIL) FAILED" -ForegroundColor Red
    Write-Host ""
    Write-Host "Ver logs del servicio con problema:" -ForegroundColor Yellow
    Write-Host "  docker compose -f docker-compose.dev.yml logs --tail=50 [nombre-servicio]" -ForegroundColor Yellow
}
Write-Host ""
exit $script:FAIL
