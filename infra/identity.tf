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
# Allows App Service to call the Azure AI Foundry account
# ─────────────────────────────────────────────
resource "azurerm_role_assignment" "openai_user" {
  scope                = azurerm_cognitive_account.foundry.id
  role_definition_name = "Cognitive Services OpenAI User"
  principal_id         = azurerm_user_assigned_identity.app.principal_id
}

# Allows the Web App to create and invoke versioned agents in this project.
resource "azurerm_role_assignment" "foundry_project_user" {
  scope                = azurerm_cognitive_account_project.foundry.id
  role_definition_name = "Foundry User"
  principal_id         = azurerm_user_assigned_identity.app.principal_id
}

# Lets human operators create/manage agents from the Foundry portal (ai.azure.com).
resource "azurerm_role_assignment" "foundry_portal_admin" {
  for_each             = toset(var.foundry_portal_admin_object_ids)
  scope                = azurerm_cognitive_account_project.foundry.id
  role_definition_name = "Foundry User"
  principal_id         = each.value
}

# ─────────────────────────────────────────────
# RBAC: Cosmos DB Built-in Data Contributor (data plane)
# Cosmos DB has its own RBAC system separate from ARM roles — an ARM
# "Contributor" role does NOT grant data access. Required for the app to
# read/write session documents using its managed identity (no keys).
# ─────────────────────────────────────────────
resource "azurerm_cosmosdb_sql_role_assignment" "app_data_contributor" {
  count               = var.enable_cosmos_db ? 1 : 0
  resource_group_name = azurerm_resource_group.main.name
  account_name        = azurerm_cosmosdb_account.main[0].name
  role_definition_id  = "${azurerm_cosmosdb_account.main[0].id}/sqlRoleDefinitions/00000000-0000-0000-0000-000000000002"
  principal_id        = azurerm_user_assigned_identity.app.principal_id
  scope               = azurerm_cosmosdb_account.main[0].id
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
# RBAC: Cognitive Services User (VoiceLive realtime models)
# Realtime API needs this in addition to the OpenAI User role already
# granted on the main Foundry account (azurerm_role_assignment.openai_user).
# ─────────────────────────────────────────────
resource "azurerm_role_assignment" "aiservices_user" {
  count                = var.enable_voicelive ? 1 : 0
  scope                = azurerm_cognitive_account.foundry.id
  role_definition_name = "Cognitive Services User"
  principal_id         = azurerm_user_assigned_identity.app.principal_id
}

# ─────────────────────────────────────────────
# RBAC: Cognitive Services User on Foundry
# Allows Search to attach the Foundry account to a skillset for keyless billing.
# ─────────────────────────────────────────────
resource "azurerm_role_assignment" "search_foundry_user" {
  count                = var.enable_ai_search ? 1 : 0
  scope                = azurerm_cognitive_account.foundry.id
  role_definition_name = "Cognitive Services User"
  principal_id         = azurerm_search_service.main[0].identity[0].principal_id
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
  scope                = azurerm_cognitive_account.foundry.id
  role_definition_name = "Cognitive Services OpenAI User"
  principal_id         = azurerm_search_service.main[0].identity[0].principal_id
}

# The Foundry project's identity can read the same RAG sources without keys.
resource "azurerm_role_assignment" "foundry_project_storage_reader" {
  scope                = azurerm_storage_account.main.id
  role_definition_name = "Storage Blob Data Reader"
  principal_id         = azurerm_cognitive_account_project.foundry.identity[0].principal_id
}

resource "azurerm_role_assignment" "foundry_project_search_reader" {
  count                = var.enable_ai_search ? 1 : 0
  scope                = azurerm_search_service.main[0].id
  role_definition_name = "Search Index Data Reader"
  principal_id         = azurerm_cognitive_account_project.foundry.identity[0].principal_id
}
