# ─────────────────────────────────────────────
# Azure Bot Service (optional)
# Uses the UAMI as Microsoft App Identity, pointed at the Web App.
# ─────────────────────────────────────────────
resource "azurerm_bot_service_azure_bot" "main" {
  count               = var.enable_bot_service ? 1 : 0
  name                = "bot-${local.unique_name}"
  resource_group_name = azurerm_resource_group.main.name
  location            = "global"
  sku                 = var.bot_sku

  microsoft_app_id        = azurerm_user_assigned_identity.app.client_id
  microsoft_app_type      = "UserAssignedMSI"
  microsoft_app_msi_id    = azurerm_user_assigned_identity.app.id
  microsoft_app_tenant_id = data.azurerm_client_config.current.tenant_id

  endpoint = "https://${azurerm_linux_web_app.main.default_hostname}/api/messages"

  tags = local.common_tags
}
