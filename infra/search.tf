# ─────────────────────────────────────────────
# Azure AI Search (optional)
# ─────────────────────────────────────────────
resource "azurerm_search_service" "main" {
  count               = var.enable_ai_search ? 1 : 0
  name                = "srch-${local.unique_name}"
  resource_group_name = azurerm_resource_group.main.name
  location            = var.ai_search_location
  sku                 = var.ai_search_sku

  semantic_search_sku = var.ai_search_sku == "free" ? null : "standard"

  local_authentication_enabled = true
  authentication_failure_mode  = "http401WithBearerChallenge"

  identity {
    type = "SystemAssigned"
  }

  tags = local.common_tags
}

# ─────────────────────────────────────────────
# Secondary AI Services (AIServices) account co-located with Azure AI Search.
# Required because the AI Search skillset's CognitiveServicesAccountKey only
# accepts a multi-service key from an account in the same region as the search
# service. The primary AI Foundry account lives in eastus2; the search service
# lives in var.ai_search_location, so it needs its own account there.
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
