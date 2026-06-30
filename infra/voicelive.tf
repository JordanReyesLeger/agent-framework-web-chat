# ─────────────────────────────────────────────
# OPTIONAL: VoiceLive — dedicated Azure AI Foundry account (kind = AIServices)
# Hosts the realtime (speech-to-speech) model used by the VoiceLive page.
# Gated behind var.enable_voicelive so the rest of the stack can deploy
# without it.
# ─────────────────────────────────────────────
resource "azurerm_cognitive_account" "voicelive" {
  count                         = var.enable_voicelive ? 1 : 0
  name                          = "aifvl-${local.unique_name}"
  resource_group_name           = azurerm_resource_group.main.name
  location                      = var.voicelive_location
  kind                          = "AIServices"
  sku_name                      = var.voicelive_sku
  custom_subdomain_name         = "aifvl-${local.unique_name}"
  public_network_access_enabled = true
  local_auth_enabled            = true

  lifecycle {
    ignore_changes = [local_auth_enabled]
  }

  tags = local.common_tags
}

# ─────────────────────────────────────────────
# Realtime model deployment (gpt-realtime-mini)
# Required by the VoiceLive page
# ─────────────────────────────────────────────
resource "azurerm_cognitive_deployment" "voicelive_realtime" {
  count                = var.enable_voicelive ? 1 : 0
  name                 = var.voicelive_model_name
  cognitive_account_id = azurerm_cognitive_account.voicelive[0].id

  model {
    format  = "OpenAI"
    name    = var.voicelive_model_name
    version = var.voicelive_model_version
  }

  sku {
    name     = "GlobalStandard"
    capacity = var.voicelive_model_capacity
  }
}
