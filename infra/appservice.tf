# ─────────────────────────────────────────────
# App Service Plan (Linux)
# eastus2 reports 0 quota for every App Service SKU in this subscription
# (Microsoft.Web usages is misleading — actual deploys 401 regardless of
# SKU). westus2 has real quota (verified: B1/S1/S2 all report limit=10,
# and a prior deployment in this same subscription actually ran a B1 plan
# there), so the Web App is hosted in westus2 instead.
# ─────────────────────────────────────────────
resource "azurerm_service_plan" "main" {
  name                = "asp-${local.unique_name}"
  resource_group_name = azurerm_resource_group.main.name
  location            = var.app_service_location
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
    always_on           = var.app_service_sku != "F1" && var.app_service_sku != "D1"
    http2_enabled       = true
    minimum_tls_version = "1.2"
    ftps_state          = "Disabled"

    application_stack {
      dotnet_version = "9.0"
    }
  }

  app_settings = merge(
    {
      # ── Managed Identity ──
      "AZURE_CLIENT_ID" = azurerm_user_assigned_identity.app.client_id

      # ── Build / runtime ──
      # Disable Oryx — we always zip-deploy pre-built artifacts from `dotnet publish`.
      "SCM_DO_BUILD_DURING_DEPLOYMENT" = "false"
      "ENABLE_ORYX_BUILD"              = "false"
      "ASPNETCORE_ENVIRONMENT"         = "Production"

      # ── Microsoft Foundry (managed identity only) ──
      "AzureOpenAI__Endpoint"            = azurerm_cognitive_account.foundry.endpoint
      "AzureOpenAI__EndpointProject"     = local.foundry_project_endpoint
      "AzureOpenAI__ChatDeployment"      = var.openai_chat_model_name
      "AzureOpenAI__EmbeddingDeployment" = var.openai_embedding_model_name
      "AZURE_AI_PROJECT_ENDPOINT"        = local.foundry_project_endpoint
      "FOUNDRY_PROJECT_ENDPOINT"         = local.foundry_project_endpoint

      # ── FoundryOrchestratorAgent's OpenAPI tool calls this app's own
      # /api/chat/send. Built from the known name pattern (not a self-
      # reference to azurerm_linux_web_app.main, which would cycle).
      "DevTunnel__Url" = "https://app-${local.unique_name}.azurewebsites.net"

      # ── Storage (managed identity; shared-key auth is disabled) ──
      "BlobStorage__ContainerName"         = "documents"
      "AzureStorage__AccountName"          = azurerm_storage_account.main.name
      "AzureStorage__ContainerName"        = "skill-documents"
      "AzureStorage__UseDefaultCredential" = "true"
      "AzureStorage__ResourceGroup"        = azurerm_resource_group.main.name

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
      "AzureSearch__LegalIndexName"     = "legal-documents"
      "AzureSearch__SkillIndexName"     = "skill"
      "AzureSearch__SemanticConfigName" = "skill-semantic-config"
      "AzureSearch__DefaultMaxResults"  = "15"
    } : {},

    # ── Cosmos DB (conditional; managed identity, no keys) ──
    var.enable_cosmos_db ? {
      "CosmosDB__AccountEndpoint" = azurerm_cosmosdb_account.main[0].endpoint
      "CosmosDB__DatabaseName"    = "af-webchat"
      "CosmosDB__ContainerName"   = "sessions"
    } : {},

    # ── Speech (key injected post-create only for Live Avatar browser tokens) ──
    var.enable_speech ? {
      "AzureSpeech__Region"              = azurerm_cognitive_account.speech[0].location
      "AzureSpeech__RecognitionLanguage" = "es-MX"
      "AzureSpeech__SynthesisVoiceName"  = "es-MX-DaliaNeural"
    } : {},

    # ── VoiceLive (optional; managed identity) ──
    var.enable_voicelive ? {
      "VoiceLive__Endpoint"  = "https://${azurerm_cognitive_account.foundry.name}.services.ai.azure.com/"
      "VoiceLive__Model"     = azurerm_cognitive_deployment.voicelive_realtime[0].name
      "VoiceLive__Models__0" = azurerm_cognitive_deployment.voicelive_realtime[0].name
      "VoiceLive__Models__1" = azurerm_cognitive_deployment.voicelive_realtime_pro[0].name
      "VoiceLive__Voice"     = "en-US-Andrew3:DragonHDLatestNeural"
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

  # Live Avatar currently requires a Speech key to mint browser and ICE tokens.
  # The focused post-deploy step injects only that key.
  lifecycle {
    ignore_changes = [
      app_settings["AzureSpeech__SubscriptionKey"],
    ]
  }
}
