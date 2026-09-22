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

output "foundry_project_name" {
  description = "Microsoft Foundry project name"
  value       = azurerm_cognitive_account_project.foundry.name
}

output "foundry_project_id" {
  description = "Microsoft Foundry project resource ID"
  value       = azurerm_cognitive_account_project.foundry.id
}

output "foundry_project_endpoint" {
  description = "Microsoft Foundry project endpoint"
  value       = local.foundry_project_endpoint
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

# ── Jumpbox / Bastion ──

output "bastion_name" {
  description = "Nombre del Azure Bastion host (si esta habilitado)"
  value       = var.enable_jumpbox ? azurerm_bastion_host.main[0].name : null
}

output "jumpbox_vm_name" {
  description = "Nombre de la VM jumpbox (si esta habilitada)"
  value       = var.enable_jumpbox ? azurerm_windows_virtual_machine.jumpbox[0].name : null
}

output "jumpbox_private_ip" {
  description = "IP privada del jumpbox, usala para conectarte via Bastion"
  value       = var.enable_jumpbox ? azurerm_network_interface.jumpbox[0].private_ip_address : null
}

output "jumpbox_admin_username" {
  description = "Usuario administrador del jumpbox"
  value       = var.enable_jumpbox ? var.jumpbox_admin_username : null
}

output "jumpbox_admin_password" {
  description = "Password generado del jumpbox. Cópialo y guárdalo aparte; no queda en el repo."
  value       = var.enable_jumpbox ? random_password.jumpbox_admin[0].result : null
  sensitive   = true
}

output "speech_service_region" {
  description = "Azure Speech Service region (if deployed)"
  value       = var.enable_speech ? azurerm_cognitive_account.speech[0].location : null
}

output "voicelive_endpoint" {
  description = "Voice Live API endpoint (served from the single Foundry account)"
  value       = var.enable_voicelive ? "https://${azurerm_cognitive_account.foundry.name}.services.ai.azure.com/" : null
}

output "voicelive_cognitive_endpoint" {
  description = "Generic Cognitive Services endpoint used for VoiceLive"
  value       = var.enable_voicelive ? azurerm_cognitive_account.foundry.endpoint : null
}

output "voicelive_model_deployments" {
  description = "VoiceLive realtime model deployments selectable by the application"
  value = var.enable_voicelive ? [
    azurerm_cognitive_deployment.voicelive_realtime[0].name,
    azurerm_cognitive_deployment.voicelive_realtime_pro[0].name
  ] : []
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

output "AZURE_SUBSCRIPTION_ID" {
  description = "Azure subscription ID for azd"
  value       = var.subscription_id
}

output "AZURE_RESOURCE_GROUP" {
  description = "Azure resource group for azd"
  value       = azurerm_resource_group.main.name
}

output "AZURE_AI_ACCOUNT_NAME" {
  description = "Microsoft Foundry account name for azd"
  value       = azurerm_cognitive_account.foundry.name
}

output "AZURE_AI_PROJECT_NAME" {
  description = "Microsoft Foundry project name for azd"
  value       = azurerm_cognitive_account_project.foundry.name
}

output "AZURE_AI_PROJECT_ENDPOINT" {
  description = "Microsoft Foundry project endpoint for azd"
  value       = local.foundry_project_endpoint
}

output "SERVICE_WEB_NAME" {
  description = "App Service name for azd web service target"
  value       = azurerm_linux_web_app.main.name
}
