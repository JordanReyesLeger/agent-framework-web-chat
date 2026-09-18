# ─────────────────────────────────────────────
# Post-deploy exception: Live Avatar currently requires a Speech key to mint
# browser authorization and ICE relay tokens. Foundry, Search, Storage, and
# VoiceLive remain keyless and use managed identities.
# ─────────────────────────────────────────────

locals {
  rg_name  = azurerm_resource_group.main.name
  app_name = azurerm_linux_web_app.main.name
}

resource "terraform_data" "inject_speech_key" {
  count = var.enable_speech ? 1 : 0

  triggers_replace = {
    web_app_id = azurerm_linux_web_app.main.id
    speech_id  = azurerm_cognitive_account.speech[0].id
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

      $account = "${azurerm_cognitive_account.speech[0].name}"
      Write-Host "[inject-speech-key] Enabling Speech local auth for Live Avatar"
      az resource update --resource-group $rg --name $account `
        --resource-type "Microsoft.CognitiveServices/accounts" `
        --set properties.disableLocalAuth=false | Out-Null
      $speechKey = az cognitiveservices account keys list -n $account -g $rg --query key1 -o tsv
      if (-not $speechKey) { throw "Empty Speech key returned" }

      Write-Host "[inject-speech-key] Updating Web App setting"
      az webapp config appsettings set -n $app -g $rg `
        --settings "AzureSpeech__SubscriptionKey=$speechKey" | Out-Null

      Write-Host "[inject-speech-key] Done"
    EOT
  }

  depends_on = [
    azurerm_linux_web_app.main,
    azurerm_cognitive_account.speech,
  ]
}
