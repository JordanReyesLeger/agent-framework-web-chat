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

# ─────────────────────────────────────────────
# RBAC: Cognitive Services User (Speech Service)
# ─────────────────────────────────────────────
resource "azurerm_role_assignment" "speech_user" {
  count                = var.enable_speech ? 1 : 0
  scope                = azurerm_cognitive_account.speech[0].id
  role_definition_name = "Cognitive Services User"
  principal_id         = azurerm_user_assigned_identity.app.principal_id
}

# ─────────────────────────────────────────────
# RBAC: Cognitive Services OpenAI User (AI Services / VoiceLive)
# ─────────────────────────────────────────────
resource "azurerm_role_assignment" "aiservices_openai_user" {
  count                = var.enable_ai_services ? 1 : 0
  scope                = azurerm_cognitive_account.aiservices[0].id
  role_definition_name = "Cognitive Services OpenAI User"
  principal_id         = azurerm_user_assigned_identity.app.principal_id
}

resource "azurerm_role_assignment" "aiservices_user" {
  count                = var.enable_ai_services ? 1 : 0
  scope                = azurerm_cognitive_account.aiservices[0].id
  role_definition_name = "Cognitive Services User"
  principal_id         = azurerm_user_assigned_identity.app.principal_id
}

# ─────────────────────────────────────────────
# RBAC: Cognitive Services User (secondary AI Services in search region)
# ─────────────────────────────────────────────
resource "azurerm_role_assignment" "aiservices_search_user" {
  count                = var.enable_ai_search ? 1 : 0
  scope                = azurerm_cognitive_account.aiservices_search[0].id
  role_definition_name = "Cognitive Services User"
  principal_id         = azurerm_user_assigned_identity.app.principal_id
}

# ─────────────────────────────────────────────
# RBAC: Storage Blob Data Reader on the storage account for the
# Azure AI Search service's own System-Assigned MI. Required so the
# indexer can read documents from blob storage using managed identity
# (storage account has shared-key auth disabled).
# ─────────────────────────────────────────────
resource "azurerm_role_assignment" "search_storage_blob_reader" {
  count                = var.enable_ai_search ? 1 : 0
  scope                = azurerm_storage_account.main.id
  role_definition_name = "Storage Blob Data Reader"
  principal_id         = azurerm_search_service.main[0].identity[0].principal_id
}

# ─────────────────────────────────────────────
# RBAC: Cognitive Services OpenAI User on the OpenAI account for the
# Search service's own System-Assigned MI. Required because the
# AzureOpenAIEmbeddingSkill in the skillset calls the OpenAI embedding
# endpoint using the search MI (no API key passed in the skill).
# ─────────────────────────────────────────────
resource "azurerm_role_assignment" "search_openai_user" {
  count                = var.enable_ai_search ? 1 : 0
  scope                = azurerm_cognitive_account.openai.id
  role_definition_name = "Cognitive Services OpenAI User"
  principal_id         = azurerm_search_service.main[0].identity[0].principal_id
}
