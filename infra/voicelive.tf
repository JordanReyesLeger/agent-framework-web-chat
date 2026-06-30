# ─────────────────────────────────────────────
# Azure AI Services account (multi-service)
# Used for VoiceLive (gpt-4o realtime models)
# kind = AIServices
# ─────────────────────────────────────────────
resource "azurerm_cognitive_account" "aiservices" {
  count                         = var.enable_ai_services ? 1 : 0
  name                          = "ais-${local.unique_name}"
  resource_group_name           = azurerm_resource_group.main.name
  location                      = var.ai_services_location
  kind                          = "AIServices"
  sku_name                      = var.ai_services_sku
  custom_subdomain_name         = "ais-${local.unique_name}"
  public_network_access_enabled = true
  local_auth_enabled            = true

  lifecycle {
    ignore_changes = [local_auth_enabled]
  }

  tags = local.common_tags
}

# ─────────────────────────────────────────────
# Secondary AI Services account in the SAME REGION as Azure AI Search.
# Required because the AI Search skillset's CognitiveServicesAccountKey
# only accepts a multi-service key from an account co-located with the
# search service (eastus). The primary AI Services account lives in
# eastus2 (only region with gpt-4o realtime model availability).
# ─────────────────────────────────────────────
resource "azurerm_cognitive_account" "aiservices_search" {
  count                         = var.enable_ai_search ? 1 : 0
  name                          = "aissrch-${local.unique_name}"
  resource_group_name           = azurerm_resource_group.main.name
  location                      = var.ai_search_location
  kind                          = "AIServices"
  sku_name                      = "S0"
  custom_subdomain_name         = "aissrch-${local.unique_name}"
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
  count                = var.enable_ai_services ? 1 : 0
  name                 = var.voicelive_model_name
  cognitive_account_id = azurerm_cognitive_account.aiservices[0].id

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
