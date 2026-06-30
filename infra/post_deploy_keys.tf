# ─────────────────────────────────────────────
# Post-deploy: re-enable local key auth on cognitive accounts and inject
# the actual keys as Web App app_settings.
#
# An MCAPS subscription policy ("CognitiveServices_LocalAuth_Modify", effect:
# modify) auto-flips disableLocalAuth=true on create, so the Azure RM key
# fields return empty during terraform refresh. This null_resource flips it
# back via az CLI, fetches the real keys, and pushes them as app_settings.
# The AF-WebChat code requires Speech and VoiceLive keys (no MI fallback).
# ─────────────────────────────────────────────

locals {
  rg_name  = azurerm_resource_group.main.name
  app_name = azurerm_linux_web_app.main.name
}

resource "null_resource" "inject_cognitive_keys" {
  triggers = {
    web_app_id            = azurerm_linux_web_app.main.id
    openai_id             = azurerm_cognitive_account.foundry.id
    speech_id             = var.enable_speech ? azurerm_cognitive_account.speech[0].id : ""
    ai_services_id        = var.enable_voicelive ? azurerm_cognitive_account.voicelive[0].id : ""
    ai_services_search_id = var.enable_ai_search ? azurerm_cognitive_account.aiservices_search[0].id : ""
    voicelive_dep_id      = var.enable_voicelive ? azurerm_cognitive_deployment.voicelive_realtime[0].id : ""
    enable_speech         = tostring(var.enable_speech)
    enable_voicelive      = tostring(var.enable_voicelive)
    enable_ai_search      = tostring(var.enable_ai_search)
  }

  provisioner "local-exec" {
    interpreter = ["pwsh", "-NoProfile", "-Command"]
    command     = <<-EOT
      $ErrorActionPreference = "Stop"
      $rg  = "${local.rg_name}"
      $app = "${local.app_name}"
      $sub = "${var.subscription_id}"

      Write-Host "[inject-keys] Setting subscription context"
      az account set --subscription $sub | Out-Null

      function UnlockAndKey([string]$accountName) {
        Write-Host "[inject-keys] Unlocking local auth on $accountName"
        az resource update --resource-group $rg --name $accountName `
          --resource-type "Microsoft.CognitiveServices/accounts" `
          --set properties.disableLocalAuth=false | Out-Null
        $k = az cognitiveservices account keys list -n $accountName -g $rg --query key1 -o tsv
        if (-not $k) { throw "Empty key returned for $accountName" }
        return $k
      }

      $settings = @()

      # OpenAI (optional fallback; MI also works)
      $openaiKey = UnlockAndKey -accountName "${azurerm_cognitive_account.foundry.name}"
      $settings += "AzureOpenAI__ApiKey=$openaiKey"

      %{if var.enable_speech~}
      $speechKey = UnlockAndKey -accountName "${azurerm_cognitive_account.speech[0].name}"
      $settings += "AzureSpeech__SubscriptionKey=$speechKey"
      %{endif~}

      %{if var.enable_voicelive~}
      $aisKey = UnlockAndKey -accountName "${azurerm_cognitive_account.voicelive[0].name}"
      $settings += "VoiceLive__ApiKey=$aisKey"
      %{endif~}

      # AzureAI__ServicesKey is consumed by the AI Search skillset, which
      # requires a multi-service Cognitive Services key co-located with the
      # search service (var.ai_search_location). Source it from the secondary
      # AI Services account, NOT from the primary (different region).
      %{if var.enable_ai_search~}
      $aisSearchKey = UnlockAndKey -accountName "${azurerm_cognitive_account.aiservices_search[0].name}"
      $settings += "AzureAI__ServicesKey=$aisSearchKey"
      %{endif~}

      Write-Host "[inject-keys] Updating Web App app_settings ($($settings.Count))"
      az webapp config appsettings set -n $app -g $rg --settings $settings | Out-Null

      Write-Host "[inject-keys] Done"
    EOT
  }

  depends_on = [
    azurerm_linux_web_app.main,
    azurerm_cognitive_account.foundry,
    azurerm_cognitive_account.speech,
    azurerm_cognitive_account.voicelive,
    azurerm_cognitive_account.aiservices_search,
    azurerm_cognitive_deployment.voicelive_realtime,
  ]
}
