# ─────────────────────────────────────────────
# Microsoft Foundry account (kind = AIServices)
# Hosts the project plus chat and embedding model deployments.
# ─────────────────────────────────────────────
resource "azurerm_cognitive_account" "foundry" {
  name                          = "aif-${local.unique_name}"
  resource_group_name           = azurerm_resource_group.main.name
  location                      = var.foundry_location
  kind                          = "AIServices"
  sku_name                      = var.foundry_sku
  custom_subdomain_name         = "aif-${local.unique_name}"
  project_management_enabled    = true
  public_network_access_enabled = false
  local_auth_enabled            = false

  identity {
    type = "SystemAssigned"
  }

  tags = local.common_tags
}

# ─────────────────────────────────────────────
# Microsoft Foundry project (new experience, not a classic hub project)
# ─────────────────────────────────────────────
resource "azurerm_cognitive_account_project" "foundry" {
  name                 = "project-${local.unique_name}"
  cognitive_account_id = azurerm_cognitive_account.foundry.id
  location             = azurerm_cognitive_account.foundry.location
  display_name         = "AF-WebChat ${var.environment_name}"
  description          = "Microsoft Foundry project for AF-WebChat agents, models, and evaluations."

  identity {
    type = "SystemAssigned"
  }

  tags = local.common_tags
}

locals {
  foundry_project_endpoint = "https://${azurerm_cognitive_account.foundry.name}.services.ai.azure.com/api/projects/${azurerm_cognitive_account_project.foundry.name}"
}

# ─────────────────────────────────────────────
# Chat Model Deployment
# ─────────────────────────────────────────────
resource "azurerm_cognitive_deployment" "chat" {
  name                 = var.openai_chat_model_name
  cognitive_account_id = azurerm_cognitive_account.foundry.id

  model {
    format  = "OpenAI"
    name    = var.openai_chat_model_name
    version = var.openai_chat_model_version
  }

  sku {
    name     = var.openai_chat_sku_name
    capacity = var.openai_chat_capacity
  }
}

# ─────────────────────────────────────────────
# Embedding Model Deployment (text-embedding-3-large)
# ─────────────────────────────────────────────
resource "azurerm_cognitive_deployment" "embedding" {
  name                 = var.openai_embedding_model_name
  cognitive_account_id = azurerm_cognitive_account.foundry.id

  model {
    format  = "OpenAI"
    name    = var.openai_embedding_model_name
    version = var.openai_embedding_model_version
  }

  sku {
    name     = var.openai_embedding_sku_name
    capacity = var.openai_embedding_capacity
  }
}
