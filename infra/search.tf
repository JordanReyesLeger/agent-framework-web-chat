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

  local_authentication_enabled = false

  # Solo se llega por el private endpoint. El indexer sale por shared private link.
  public_network_access_enabled = false

  identity {
    type = "SystemAssigned"
  }

  tags = local.common_tags
}
