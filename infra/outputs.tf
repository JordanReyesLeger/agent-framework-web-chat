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

output "openai_endpoint" {
  description = "Azure OpenAI endpoint"
  value       = azurerm_cognitive_account.openai.endpoint
}

output "openai_account_name" {
  description = "Azure OpenAI account name"
  value       = azurerm_cognitive_account.openai.name
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
