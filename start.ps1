# ============================================================
#  Marketplace — Script de démarrage PowerShell
#  Usage: .\start.ps1
# ============================================================

param(
    [switch]$Stop,
    [switch]$Restart,
    [switch]$Logs,
    [switch]$Status,
    [switch]$Clean
)

$ErrorActionPreference = "Stop"

function Write-Step($msg) { Write-Host "`n>> $msg" -ForegroundColor Cyan }
function Write-OK($msg)   { Write-Host "   [OK] $msg" -ForegroundColor Green }
function Write-Warn($msg) { Write-Host "   [!!] $msg" -ForegroundColor Yellow }
function Write-Err($msg)  { Write-Host "   [XX] $msg" -ForegroundColor Red }

# ── Stop ──────────────────────────────────────────────────
if ($Stop) {
    Write-Step "Arrêt de tous les services..."
    docker-compose down
    Write-OK "Services arrêtés"
    exit 0
}

# ── Clean ─────────────────────────────────────────────────
if ($Clean) {
    Write-Step "Nettoyage complet (conteneurs + volumes + images)..."
    docker-compose down -v --rmi local
    Write-OK "Nettoyage terminé"
    exit 0
}

# ── Status ────────────────────────────────────────────────
if ($Status) {
    docker-compose ps
    exit 0
}

# ── Logs ──────────────────────────────────────────────────
if ($Logs) {
    docker-compose logs -f --tail=50
    exit 0
}

# ── Start / Restart ───────────────────────────────────────
Write-Host ""
Write-Host "  ╔══════════════════════════════════════╗" -ForegroundColor Magenta
Write-Host "  ║     🛒  MARKETPLACE MICROSERVICES    ║" -ForegroundColor Magenta
Write-Host "  ╚══════════════════════════════════════╝" -ForegroundColor Magenta
Write-Host ""

# Check Docker
Write-Step "Vérification de Docker..."
try {
    $dockerVersion = docker --version 2>&1
    Write-OK $dockerVersion
} catch {
    Write-Err "Docker n'est pas installé ou pas démarré. Lancez Docker Desktop d'abord."
    exit 1
}

# Check docker-compose
try {
    docker compose version 2>&1 | Out-Null
    $COMPOSE = "docker compose"
} catch {
    try {
        docker-compose --version 2>&1 | Out-Null
        $COMPOSE = "docker-compose"
    } catch {
        Write-Err "docker-compose introuvable."
        exit 1
    }
}
Write-OK "docker compose disponible"

# Restart mode
if ($Restart) {
    Write-Step "Redémarrage..."
    Invoke-Expression "$COMPOSE down"
}

# ── STEP 1: Build images ──────────────────────────────────
Write-Step "ETAPE 1/4 — Build des images Docker..."
Write-Host "   (peut prendre 3-5 min au premier lancement)" -ForegroundColor Gray
Invoke-Expression "$COMPOSE build --parallel"
Write-OK "Images buildées"

# ── STEP 2: Start infrastructure ─────────────────────────
Write-Step "ETAPE 2/4 — Démarrage des bases de données..."
Invoke-Expression "$COMPOSE up -d productdb orderdb neo4j redis rabbitmq"
Write-OK "Infrastructure démarrée"

# ── STEP 3: Wait for healthchecks ────────────────────────
Write-Step "ETAPE 3/4 — Attente des healthchecks (30s)..."
Write-Host "   MongoDB, Neo4j, RabbitMQ..." -ForegroundColor Gray

$waited = 0
$maxWait = 90
do {
    Start-Sleep -Seconds 5
    $waited += 5
    $mongoReady  = (docker inspect --format="{{.State.Health.Status}}" productdb 2>$null) -eq "healthy"
    $rabbitReady = (docker inspect --format="{{.State.Health.Status}}" rabbitmq  2>$null) -eq "healthy"
    $neo4jReady  = (docker inspect --format="{{.State.Health.Status}}" neo4j     2>$null) -eq "healthy"

    $status = "   [$waited`s] MongoDB:"
    $status += if ($mongoReady)  { " ✓" } else { " ..." }
    $status += "  RabbitMQ:"
    $status += if ($rabbitReady) { " ✓" } else { " ..." }
    $status += "  Neo4j:"
    $status += if ($neo4jReady)  { " ✓" } else { " ..." }
    Write-Host $status -ForegroundColor Gray

} while ((-not ($mongoReady -and $rabbitReady -and $neo4jReady)) -and ($waited -lt $maxWait))

if (-not ($mongoReady -and $rabbitReady)) {
    Write-Warn "Certains services ne sont pas encore healthy. Continuation quand même..."
} else {
    Write-OK "Infrastructure prête !"
}

# ── STEP 4: Start microservices ───────────────────────────
Write-Step "ETAPE 4/4 — Démarrage des microservices..."
Invoke-Expression "$COMPOSE up -d product.api order.api recommendation.api apigateway"
Write-OK "Microservices démarrés"

# Wait a bit for services to initialize
Start-Sleep -Seconds 10

# ── Health checks ─────────────────────────────────────────
Write-Step "Vérification des services..."

$services = @(
    @{ Name = "Product API";        Url = "http://localhost:8001/health"; Port = 8001 },
    @{ Name = "Order API";          Url = "http://localhost:8004/health"; Port = 8004 },
    @{ Name = "Recommendation API"; Url = "http://localhost:8005/health"; Port = 8005 },
    @{ Name = "API Gateway";        Url = "http://localhost:8000/health"; Port = 8000 }
)

foreach ($svc in $services) {
    try {
        $resp = Invoke-WebRequest -Uri $svc.Url -TimeoutSec 5 -UseBasicParsing 2>$null
        if ($resp.StatusCode -eq 200) {
            Write-OK "$($svc.Name) → http://localhost:$($svc.Port)"
        } else {
            Write-Warn "$($svc.Name) → Status $($resp.StatusCode)"
        }
    } catch {
        Write-Warn "$($svc.Name) → Pas encore prêt (démarrage en cours...)"
    }
}

# ── Summary ───────────────────────────────────────────────
Write-Host ""
Write-Host "  ╔══════════════════════════════════════════════════════════╗" -ForegroundColor Green
Write-Host "  ║              🚀  MARKETPLACE EN LIGNE !                  ║" -ForegroundColor Green
Write-Host "  ╠══════════════════════════════════════════════════════════╣" -ForegroundColor Green
Write-Host "  ║                                                          ║" -ForegroundColor Green
Write-Host "  ║  API Gateway         http://localhost:8000               ║" -ForegroundColor Green
Write-Host "  ║  Product Swagger     http://localhost:8001/swagger       ║" -ForegroundColor Green
Write-Host "  ║  Order Swagger       http://localhost:8004/swagger       ║" -ForegroundColor Green
Write-Host "  ║  Recomm. Swagger     http://localhost:8005/swagger       ║" -ForegroundColor Green
Write-Host "  ║                                                          ║" -ForegroundColor Green
Write-Host "  ║  RabbitMQ UI         http://localhost:15672              ║" -ForegroundColor Green
Write-Host "  ║                      (guest / guest)                     ║" -ForegroundColor Green
Write-Host "  ║  Neo4j Browser       http://localhost:7474               ║" -ForegroundColor Green
Write-Host "  ║                      (neo4j / password123)               ║" -ForegroundColor Green
Write-Host "  ║  Portainer           http://localhost:9443               ║" -ForegroundColor Green
Write-Host "  ║                                                          ║" -ForegroundColor Green
Write-Host "  ╠══════════════════════════════════════════════════════════╣" -ForegroundColor Green
Write-Host "  ║  Commandes utiles:                                       ║" -ForegroundColor Green
Write-Host "  ║    .\start.ps1 -Status    → voir état des conteneurs     ║" -ForegroundColor Green
Write-Host "  ║    .\start.ps1 -Logs      → voir les logs en direct      ║" -ForegroundColor Green
Write-Host "  ║    .\start.ps1 -Stop      → arrêter tout                 ║" -ForegroundColor Green
Write-Host "  ║    .\start.ps1 -Restart   → redémarrer tout              ║" -ForegroundColor Green
Write-Host "  ║    .\start.ps1 -Clean     → tout supprimer (avec volumes)║" -ForegroundColor Green
Write-Host "  ╚══════════════════════════════════════════════════════════╝" -ForegroundColor Green
Write-Host ""
