# ─────────────────────────────────────────────
# Azure AI Foundry account (kind = AIServices)
# Hosts the chat (gpt-4o) and embedding (text-embedding-3-large) models.
# This replaces the legacy classic Azure OpenAI (kind = "OpenAI") account:
# the app talks to it through the same /openai data-plane API, so the
# AzureOpenAIClient keeps working against the *.cognitiveservices.azure.com
# (Foundry) endpoint.
# ─────────────────────────────────────────────
resource "azurerm_cognitive_account" "foundry" {
  name                          = "aif-${local.unique_name}"
  resource_group_name           = azurerm_resource_group.main.name
  location                      = azurerm_resource_group.main.location
  kind                          = "AIServices"
  sku_name                      = var.foundry_sku
  custom_subdomain_name         = "aif-${local.unique_name}"
  public_network_access_enabled = true
  local_auth_enabled            = true # API key fallback; RBAC is primary

  # An MCAPS policy auto-disables local auth on create; null_resource.inject_cognitive_keys re-enables it.
  lifecycle {
    ignore_changes = [local_auth_enabled]
  }

  tags = local.common_tags
}

# ─────────────────────────────────────────────
# Chat Model Deployment (gpt-4o)
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
    name     = "Standard"
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
    name     = "Standard"
    capacity = var.openai_embedding_capacity
  }
}
