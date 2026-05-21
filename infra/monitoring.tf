# ─────────────────────────────────────────────
# Log Analytics Workspace
# ─────────────────────────────────────────────
resource "azurerm_log_analytics_workspace" "main" {
  name                = "log-${local.unique_name}"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  sku                 = "PerGB2018"
  retention_in_days   = 30
  tags                = local.common_tags
}

# ─────────────────────────────────────────────
# Application Insights
# ─────────────────────────────────────────────
resource "azurerm_application_insights" "main" {
  name                = "appi-${local.unique_name}"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  workspace_id        = azurerm_log_analytics_workspace.main.id
  application_type    = "web"
  tags                = local.common_tags
}
