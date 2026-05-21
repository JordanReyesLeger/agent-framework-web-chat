# ─────────────────────────────────────────────
# User-Assigned Managed Identity
# Used by App Service for passwordless access
# ─────────────────────────────────────────────
resource "azurerm_user_assigned_identity" "app" {
  name                = "id-${local.unique_name}"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  tags                = local.common_tags
}

# ─────────────────────────────────────────────
# RBAC: Cognitive Services OpenAI User
# Allows App Service to call Azure OpenAI
# ─────────────────────────────────────────────
resource "azurerm_role_assignment" "openai_user" {
  scope                = azurerm_cognitive_account.openai.id
  role_definition_name = "Cognitive Services OpenAI User"
  principal_id         = azurerm_user_assigned_identity.app.principal_id
}

# ─────────────────────────────────────────────
# RBAC: Storage Blob Data Contributor
# Allows App Service to read/write blobs
# ─────────────────────────────────────────────
resource "azurerm_role_assignment" "storage_blob" {
  scope                = azurerm_storage_account.main.id
  role_definition_name = "Storage Blob Data Contributor"
  principal_id         = azurerm_user_assigned_identity.app.principal_id
}

# ─────────────────────────────────────────────
# RBAC: Search Index Data Contributor (conditional)
# Allows App Service to query/manage search indexes
# ─────────────────────────────────────────────
resource "azurerm_role_assignment" "search_contributor" {
  count                = var.enable_ai_search ? 1 : 0
  scope                = azurerm_search_service.main[0].id
  role_definition_name = "Search Index Data Contributor"
  principal_id         = azurerm_user_assigned_identity.app.principal_id
}

resource "azurerm_role_assignment" "search_service_contributor" {
  count                = var.enable_ai_search ? 1 : 0
  scope                = azurerm_search_service.main[0].id
  role_definition_name = "Search Service Contributor"
  principal_id         = azurerm_user_assigned_identity.app.principal_id
}
