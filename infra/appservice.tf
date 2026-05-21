# ─────────────────────────────────────────────
# App Service Plan (Linux)
# ─────────────────────────────────────────────
resource "azurerm_service_plan" "main" {
  name                = "asp-${local.unique_name}"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  os_type             = "Linux"
  sku_name            = var.app_service_sku
  tags                = local.common_tags
}

# ─────────────────────────────────────────────
# Linux Web App (.NET 9)
# ─────────────────────────────────────────────
resource "azurerm_linux_web_app" "main" {
  name                = "app-${local.unique_name}"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_service_plan.main.location
  service_plan_id     = azurerm_service_plan.main.id

  https_only = true

  identity {
    type         = "SystemAssigned, UserAssigned"
    identity_ids = [azurerm_user_assigned_identity.app.id]
  }

  site_config {
    always_on         = var.app_service_sku != "F1" && var.app_service_sku != "D1"
    http2_enabled     = true
    minimum_tls_version = "1.2"

    application_stack {
      dotnet_version = "9.0"
    }
  }

  app_settings = merge(
    {
      # ── Managed Identity ──
      "AZURE_CLIENT_ID" = azurerm_user_assigned_identity.app.client_id

      # ── Azure OpenAI ──
      "AzureOpenAI__Endpoint"            = azurerm_cognitive_account.openai.endpoint
      "AzureOpenAI__ChatDeployment"       = var.openai_chat_model_name
      "AzureOpenAI__EmbeddingDeployment"  = var.openai_embedding_model_name
      "AzureOpenAI__ApiKey"               = azurerm_cognitive_account.openai.primary_access_key

      # ── Storage ──
      "BlobStorage__ConnectionString"   = azurerm_storage_account.main.primary_connection_string
      "BlobStorage__ContainerName"      = "documents"
      "AzureStorage__AccountName"       = azurerm_storage_account.main.name
      "AzureStorage__ContainerName"     = "skill-documents"
      "AzureStorage__UseDefaultCredential" = "true"
      "AzureStorage__ConnectionString"  = azurerm_storage_account.main.primary_connection_string
      "AzureStorage__ResourceGroup"     = azurerm_resource_group.main.name

      # ── Azure Subscription Info ──
      "Azure__SubscriptionId" = var.subscription_id
      "Azure__ResourceGroup"  = azurerm_resource_group.main.name
      "Azure__TenantId"       = data.azurerm_client_config.current.tenant_id

      # ── Application Insights ──
      "APPLICATIONINSIGHTS_CONNECTION_STRING" = azurerm_application_insights.main.connection_string
    },

    # ── AI Search (conditional) ──
    var.enable_ai_search ? {
      "AzureSearch__Endpoint"           = "https://${azurerm_search_service.main[0].name}.search.windows.net"
      "AzureSearch__IndexName"          = "skill"
      "AzureSearch__ApiKey"             = azurerm_search_service.main[0].primary_key
      "AzureSearch__LegalIndexName"     = "legal-documents"
      "AzureSearch__SkillIndexName"     = "skill"
      "AzureSearch__SemanticConfigName" = "skill-semantic-config"
      "AzureSearch__DefaultMaxResults"  = "15"
    } : {},

    # ── Cosmos DB (conditional) ──
    var.enable_cosmos_db ? {
      "CosmosDB__ConnectionString" = azurerm_cosmosdb_account.main[0].primary_sql_connection_string
      "CosmosDB__DatabaseName"     = "af-webchat"
      "CosmosDB__ContainerName"    = "sessions"
    } : {},
  )

  logs {
    application_logs {
      file_system_level = "Information"
    }
    http_logs {
      file_system {
        retention_in_days = 7
        retention_in_mb   = 35
      }
    }
  }

  tags = local.common_tags
}
