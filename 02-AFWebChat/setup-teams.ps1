##############################################################################
# AF-WebChat — Setup Teams Bot con Dev Tunnel
#
# USO:
#   .\setup-teams.ps1                  # Primera vez: crea tunnel + zip + arranca
#   .\setup-teams.ps1 -TunnelUrl "https://xxxxx.devtunnels.ms"  # Reusar tunnel existente
#   .\setup-teams.ps1 -ZipOnly         # Solo regenerar el zip (cuando cambias el manifest)
#   .\setup-teams.ps1 -RunOnly         # Solo ejecutar la app (tunnel ya configurado)
#
##############################################################################

param(
    [string]$TunnelUrl = "",
    [switch]$ZipOnly,
    [switch]$RunOnly,
    [int]$Port = 7071
)

$ErrorActionPreference = "Stop"

# ---- Configuración ----
$ClientId    = "<<YOUR_CLIENT_ID>>"
$TenantId    = "<<YOUR_TENANT_ID>>"
$BotName     = "<<YOUR_BOT_NAME>>"
$SubId       = "<<YOUR_SUBSCRIPTION_ID>>"
$ProjectRoot = $PSScriptRoot
$ManifestDir = Join-Path $ProjectRoot "appManifest"
$ManifestFile = Join-Path $ManifestDir "manifest.json"
$ZipFile     = Join-Path $ProjectRoot "af-webchat-teams.zip"

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  AF-WebChat — Teams Bot Setup" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# ---- Funciones ----

function Update-Manifest {
    param([string]$Domain)

    Write-Host "[1/4] Actualizando manifest.json con dominio: $Domain" -ForegroundColor Yellow

    $template = @"
{
  "`$schema": "https://developer.microsoft.com/json-schemas/teams/v1.22/MicrosoftTeams.schema.json",
  "manifestVersion": "1.22",
  "version": "1.0.0",
  "id": "$ClientId",
  "developer": {
    "name": "AF-WebChat",
    "websiteUrl": "https://$Domain",
    "privacyUrl": "https://$Domain/privacy",
    "termsOfUseUrl": "https://$Domain/termsofuse"
  },
  "icons": {
    "color": "color.png",
    "outline": "outline.png"
  },
  "name": {
    "short": "AF-WebChat",
    "full": "AF-WebChat Multi-Agent Assistant"
  },
  "description": {
    "short": "Multi-agent AI assistant powered by Microsoft Agent Framework",
    "full": "AF-WebChat is a multi-agent AI assistant with specialized agents for SQL, Legal, INEGI, Code Review and more. Use /agents to see all available agents."
  },
  "accentColor": "#C9A227",
  "copilotAgents": {
    "customEngineAgents": [
      {
        "id": "$ClientId",
        "type": "bot"
      }
    ]
  },
  "bots": [
    {
      "botId": "$ClientId",
      "scopes": ["personal", "team", "groupChat"],
      "supportsFiles": false,
      "isNotificationOnly": false,
      "commandLists": [
        {
          "scopes": ["personal", "team", "groupChat"],
          "commands": [
            { "title": "/agents",  "description": "List all available AI agents" },
            { "title": "/agent",   "description": "Switch to a specific agent (e.g., /agent LegalAdvisor)" },
            { "title": "/clear",   "description": "Clear chat history for the current agent" },
            { "title": "/new",     "description": "Start a fresh conversation (reset agent and history)" },
            { "title": "/help",    "description": "Show available commands" }
          ]
        }
      ]
    }
  ],
  "permissions": ["identity", "messageTeamMembers"],
  "validDomains": ["$Domain"]
}
"@
    $template | Set-Content -Path $ManifestFile -Encoding UTF8
    Write-Host "   manifest.json actualizado" -ForegroundColor Green
}

function New-TeamsZip {
    Write-Host "[2/4] Generando paquete Teams (.zip)..." -ForegroundColor Yellow

    if (Test-Path $ZipFile) { Remove-Item $ZipFile -Force }

    # Validar que exiten los archivos
    $requiredFiles = @("manifest.json", "color.png", "outline.png")
    foreach ($f in $requiredFiles) {
        $path = Join-Path $ManifestDir $f
        if (-not (Test-Path $path)) {
            Write-Host "   ERROR: Falta $f en appManifest/" -ForegroundColor Red
            exit 1
        }
    }

    Compress-Archive -Path (Join-Path $ManifestDir "manifest.json"),
                          (Join-Path $ManifestDir "color.png"),
                          (Join-Path $ManifestDir "outline.png") `
                     -DestinationPath $ZipFile -Force

    Write-Host "   Creado: $ZipFile" -ForegroundColor Green
}

function Update-AzureBotEndpoint {
    param([string]$Domain)

    Write-Host "[3/4] Actualizando messaging endpoint en Azure Bot..." -ForegroundColor Yellow
    $endpoint = "https://$Domain/api/messages"

    try {
        $rg = "<<YOUR_RESOURCE_GROUP>>"
        az bot update --name $BotName `
                      --resource-group $rg `
                      --subscription $SubId `
                      --endpoint $endpoint `
                      --output none 2>$null

        if ($LASTEXITCODE -eq 0) {
            Write-Host "   Endpoint actualizado: $endpoint" -ForegroundColor Green
        } else {
            Write-Host "   WARN: No se pudo actualizar automaticamente. Hazlo manual:" -ForegroundColor Yellow
            Write-Host "   Azure Portal > $BotName > Settings > Configuration > Messaging endpoint" -ForegroundColor Yellow
            Write-Host "   Valor: $endpoint" -ForegroundColor White
        }
    } catch {
        Write-Host "   WARN: az CLI no disponible o sin permisos. Actualiza manualmente:" -ForegroundColor Yellow
        Write-Host "   Azure Portal > $BotName > Settings > Configuration > Messaging endpoint" -ForegroundColor Yellow
        Write-Host "   Valor: $endpoint" -ForegroundColor White
    }
}

function Start-App {
    Write-Host "[4/4] Arrancando AF-WebChat en puerto $Port..." -ForegroundColor Yellow
    Write-Host ""
    Write-Host "   Web Chat:      https://localhost:$Port/Home/Chat" -ForegroundColor White
    Write-Host "   Agent Chat:    https://localhost:$Port/Home/AgentChat" -ForegroundColor White
    Write-Host "   Publishing:    https://localhost:$Port/Home/PublishingDemo" -ForegroundColor White
    Write-Host "   Notifications: https://localhost:$Port/Home/Notifications" -ForegroundColor White
    Write-Host ""
    Write-Host "   Presiona Ctrl+C para detener" -ForegroundColor DarkGray
    Write-Host ""

    Push-Location $ProjectRoot
    dotnet run --urls "https://localhost:$Port"
    Pop-Location
}

# ---- Main ----

if ($ZipOnly) {
    # Solo regenerar zip con el manifest actual
    $content = Get-Content $ManifestFile -Raw
    if ($content -match "<<BOT_DOMAIN>>|<<AAD_APP_CLIENT_ID>>") {
        Write-Host "ERROR: manifest.json aun tiene placeholders. Ejecuta el setup completo primero." -ForegroundColor Red
        exit 1
    }
    New-TeamsZip
    Write-Host ""
    Write-Host "Listo! Sube $ZipFile a Teams." -ForegroundColor Green
    exit 0
}

if ($RunOnly) {
    Start-App
    exit 0
}

# --- Setup completo ---

# Paso 1: Obtener o crear Dev Tunnel
if ([string]::IsNullOrEmpty($TunnelUrl)) {
    Write-Host ""
    Write-Host "Necesitas un Dev Tunnel. Hay dos opciones:" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "  Opcion 1 — Crear tunnel ahora (en otra terminal):" -ForegroundColor White
    Write-Host "    devtunnel host -p $Port --allow-anonymous" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "  Opcion 2 — Ya tienes uno corriendo" -ForegroundColor White
    Write-Host ""
    $TunnelUrl = Read-Host "Pega la URL del tunnel (ej: https://xxxxx.devtunnels.ms)"
}

# Limpiar URL
$TunnelUrl = $TunnelUrl.TrimEnd('/')
if ($TunnelUrl -match "^https?://(.+)$") {
    $Domain = $Matches[1]
} else {
    $Domain = $TunnelUrl
}

Write-Host ""
Write-Host "Configurando con dominio: $Domain" -ForegroundColor Cyan
Write-Host ""

# Ejecutar pasos
Update-Manifest -Domain $Domain
New-TeamsZip
Update-AzureBotEndpoint -Domain $Domain
Start-App
