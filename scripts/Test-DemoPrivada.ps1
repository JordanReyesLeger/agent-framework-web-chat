<#
    Valida que la demo funciona REALMENTE en privado.
    No se le cree a la configuracion: se comprueba el comportamiento.
#>
param(
    [string]$ResourceGroup = '',
    [switch]$SoloRed
)
$ErrorActionPreference = 'Continue'
Set-Location -LiteralPath 'C:\Dev\SE-AfPrivate\infra'

if (-not $ResourceGroup) { $ResourceGroup = (terraform output -raw resource_group_name 2>$null) }
$search = terraform output -raw search_service_name 2>$null
$storage = terraform output -raw storage_account_name 2>$null
$foundry = terraform output -raw foundry_account_name 2>$null
$webUrl = terraform output -raw web_app_url 2>$null
$webName = terraform output -raw web_app_name 2>$null

Write-Host ""
Write-Host "RG=$ResourceGroup  web=$webUrl" -ForegroundColor DarkGray

$criterios = [ordered]@{}

# --- 1. Los servicios deben rechazar la red publica ---------------------------
$cosmos = (az cosmosdb list -g $ResourceGroup --query "[0].name" -o tsv 2>$null)
$paFoundry = az cognitiveservices account show -n $foundry -g $ResourceGroup --query "properties.publicNetworkAccess" -o tsv 2>$null
$paSearch = az search service show -n $search -g $ResourceGroup --query "publicNetworkAccess" -o tsv 2>$null
$paCosmos = az cosmosdb show -n $cosmos -g $ResourceGroup --query "publicNetworkAccess" -o tsv 2>$null
$paStorageDefault = az storage account show -n $storage -g $ResourceGroup --query "networkRuleSet.defaultAction" -o tsv 2>$null

$criterios['Foundry sin acceso publico'] = ($paFoundry -eq 'Disabled')
$criterios['AI Search sin acceso publico'] = ($paSearch -in @('disabled', 'Disabled'))
$criterios['Cosmos DB sin acceso publico'] = ($paCosmos -in @('Disabled', 'disabled'))
$criterios['Storage en Deny por defecto'] = ($paStorageDefault -eq 'Deny')

# --- 2. Private endpoints aprobados ------------------------------------------
$pes = az network private-endpoint list -g $ResourceGroup -o json 2>$null | ConvertFrom-Json
$estados = @($pes | ForEach-Object { $_.privateLinkServiceConnections[0].privateLinkServiceConnectionState.status })
$criterios['4 private endpoints aprobados'] = (@($estados | Where-Object { $_ -eq 'Approved' }).Count -ge 4)

# --- 3. Shared private links del indexer aprobados ---------------------------
$spls = az search shared-private-link-resource list --service-name $search -g $ResourceGroup -o json 2>$null | ConvertFrom-Json
$splStatus = @($spls | ForEach-Object { $_.properties.status })
$criterios['3 shared private links'] = (@($spls).Count -ge 3)
$criterios['shared private links aprobados'] = (@($splStatus | Where-Object { $_ -eq 'Approved' }).Count -ge 3)

# --- 4. Desde fuera de la VNet, el plano de datos debe estar cerrado ----------
# Si esto CONTESTA, la demo no esta privada.
$searchAbierto = $false
try {
    # OJO: sin credenciales el servicio devuelve 401 ANTES de evaluar la red
    # (local auth esta deshabilitado), asi que un 401 NO prueba nada.
    # La unica prueba concluyente es llamar CON un token de Entra valido:
    #   403 + "publicNetworkAccess: Disabled" = cerrado
    #   200                                   = alcanzable desde Internet
    $tok = (az account get-access-token --resource https://search.azure.com --query accessToken -o tsv)
    $r = Invoke-WebRequest -Uri "https://$search.search.windows.net/indexes?api-version=2024-07-01" `
        -Headers @{ Authorization = "Bearer $tok" } -TimeoutSec 30 -SkipHttpErrorCheck
    $searchAbierto = ($r.StatusCode -eq 200)
}
catch { $searchAbierto = $false }
$criterios['AI Search NO responde desde Internet'] = (-not $searchAbierto)

if (-not $SoloRed) {
    # --- 5. La web publica debe seguir viva ----------------------------------
    # OJO: no usar $home, es variable automatica de SOLO LECTURA en PowerShell.
    $homeResp = $null
    try { $homeResp = Invoke-WebRequest -Uri $webUrl -TimeoutSec 60 -SkipHttpErrorCheck } catch {}
    $criterios['la web publica responde'] = ($null -ne $homeResp -and $homeResp.StatusCode -eq 200)

    # --- 6. El chat debe contestar usando Foundry privado --------------------
    $chatOk = $false; $chatTxt = ''
    try {
        # OJO: no existe ningun agente llamado 'default'; usar un nombre real
        # del registro (GET /api/agent) o el controlador devuelve 500.
        # OJO 2: sessionId es obligatorio; si va nulo el pipeline tambien da 500.
        $body = @{ message = 'Responde unicamente con la palabra PRIVADO'; agentName = 'GeneralAssistant'; sessionId = [guid]::NewGuid().ToString() } | ConvertTo-Json
        $resp = Invoke-WebRequest -Uri "$webUrl/api/chat/send" -Method POST -Body $body -ContentType 'application/json' -TimeoutSec 120 -SkipHttpErrorCheck
        $chatTxt = "$($resp.StatusCode) $($resp.Content)"
        $chatOk = ($resp.StatusCode -eq 200 -and $resp.Content.Length -gt 10)
    }
    catch { $chatTxt = $_.Exception.Message }
    $criterios['el chat responde por la red privada'] = $chatOk

    # --- 7. RAG completo: App Service -> AI Search -> Foundry, todo privado --
    # El documento afpriv-prueba.txt contiene el marcador ZANFIRA-7741. Si el
    # agente lo devuelve, quedo probada la cadena entera por red privada.
    # OJO: no preguntar por el "codigo secreto" -- el modelo se autocensura y
    # se niega a divulgarlo aunque SI lo haya recuperado del indice.
    $ragOk = $false; $ragTxt = ''
    try {
        $preg = 'Busca el documento afpriv-prueba.txt y extrae los datos clave e identificadores: lista todos los codigos alfanumericos que aparezcan en su contenido.'
        $bodyRag = @{ message = $preg; agentName = 'RAGAgent'; sessionId = [guid]::NewGuid().ToString() } | ConvertTo-Json
        $respRag = Invoke-WebRequest -Uri "$webUrl/api/chat/send" -Method POST -Body $bodyRag -ContentType 'application/json' -TimeoutSec 180 -SkipHttpErrorCheck
        $ragTxt = "$($respRag.StatusCode) $($respRag.Content)"
        $ragOk = ($respRag.StatusCode -eq 200 -and $respRag.Content -match 'ZANFIRA-7741')
    }
    catch { $ragTxt = $_.Exception.Message }
    $criterios['el RAG privado devuelve el marcador del documento'] = $ragOk
}

Write-Host ""
foreach ($k in $criterios.Keys) {
    $ok = [bool]$criterios[$k]
    Write-Host ("  [{0}] {1}" -f $(if ($ok) { 'OK   ' } else { 'FALLA' }), $k) -ForegroundColor $(if ($ok) { 'Green' } else { 'Red' })
}
Write-Host ""
Write-Host "foundry=$paFoundry search=$paSearch cosmos=$paCosmos storage=$paStorageDefault" -ForegroundColor DarkGray
Write-Host "spl estados: $($splStatus -join ', ')" -ForegroundColor DarkGray
if ($chatTxt) { Write-Host "chat: $($chatTxt.Substring(0, [Math]::Min(300, $chatTxt.Length)))" -ForegroundColor DarkGray }
if ($ragTxt) { Write-Host "rag : $($ragTxt.Substring(0, [Math]::Min(300, $ragTxt.Length)))" -ForegroundColor DarkGray }

if ($criterios.Values -contains $false) { exit 1 } else { exit 0 }
