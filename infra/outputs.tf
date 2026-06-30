# ─────────────────────────────────────────────
# Outputs
# ─────────────────────────────────────────────

output "resource_group_name" {
  description = "Name of the resource group"
  value       = azurerm_resource_group.main.name
}

output "web_app_name" {
  description = "Name of the App Service"
  value       = azurerm_linux_web_app.main.name
}

output "web_app_url" {
  description = "URL of the deployed web application"
  value       = "https://${azurerm_linux_web_app.main.default_hostname}"
}

output "app_service_plan_name" {
  description = "App Service Plan name"
  value       = azurerm_service_plan.main.name
}

output "foundry_endpoint" {
  description = "Azure AI Foundry (AIServices) endpoint used for chat + embeddings"
  value       = azurerm_cognitive_account.foundry.endpoint
}

output "foundry_account_name" {
  description = "Azure AI Foundry account name"
  value       = azurerm_cognitive_account.foundry.name
}

output "storage_account_name" {
  description = "Storage account name"
  value       = azurerm_storage_account.main.name
}

output "application_insights_name" {
  description = "Application Insights resource name"
  value       = azurerm_application_insights.main.name
}

output "managed_identity_client_id" {
  description = "Client ID of the User-Assigned Managed Identity"
  value       = azurerm_user_assigned_identity.app.client_id
}

# ── Conditional outputs ──

output "search_service_name" {
  description = "AI Search service name (if deployed)"
  value       = var.enable_ai_search ? azurerm_search_service.main[0].name : null
}

output "cosmosdb_endpoint" {
  description = "Cosmos DB endpoint (if deployed)"
  value       = var.enable_cosmos_db ? azurerm_cosmosdb_account.main[0].endpoint : null
}

output "speech_service_endpoint" {
  description = "Azure Speech Service endpoint (if deployed)"
  value       = var.enable_speech ? azurerm_cognitive_account.speech[0].endpoint : null
}

output "speech_service_region" {
  description = "Azure Speech Service region (if deployed)"
  value       = var.enable_speech ? azurerm_cognitive_account.speech[0].location : null
}

output "voicelive_endpoint" {
  description = "VoiceLive Azure AI Foundry endpoint (if deployed)"
  value       = var.enable_voicelive ? azurerm_cognitive_account.voicelive[0].endpoint : null
}

output "bot_service_name" {
  description = "Azure Bot Service name (if deployed)"
  value       = var.enable_bot_service ? azurerm_bot_service_azure_bot.main[0].name : null
}

output "sql_server_fqdn" {
  description = "SQL Server FQDN (if deployed)"
  value       = var.enable_sql_database ? azurerm_mssql_server.main[0].fully_qualified_domain_name : null
}

output "sql_database_name" {
  description = "SQL Database name (if deployed)"
  value       = var.enable_sql_database ? azurerm_mssql_database.main[0].name : null
}

# ── azd required outputs ──

output "AZURE_LOCATION" {
  description = "Azure region for azd"
  value       = var.location
}

output "AZURE_TENANT_ID" {
  description = "Tenant ID for azd"
  value       = data.azurerm_client_config.current.tenant_id
}

output "SERVICE_WEB_NAME" {
  description = "App Service name for azd web service target"
  value       = azurerm_linux_web_app.main.name
}
