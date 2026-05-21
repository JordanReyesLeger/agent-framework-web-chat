# ─────────────────────────────────────────────
# Azure OpenAI Account
# ─────────────────────────────────────────────
resource "azurerm_cognitive_account" "openai" {
  name                          = "oai-${local.unique_name}"
  resource_group_name           = azurerm_resource_group.main.name
  location                      = azurerm_resource_group.main.location
  kind                          = "OpenAI"
  sku_name                      = var.openai_sku
  custom_subdomain_name         = "oai-${local.unique_name}"
  public_network_access_enabled = true
  local_auth_enabled            = true # API key fallback; RBAC is primary

  tags = local.common_tags
}

# ─────────────────────────────────────────────
# Chat Model Deployment (gpt-4o)
# ─────────────────────────────────────────────
resource "azurerm_cognitive_deployment" "chat" {
  name                 = var.openai_chat_model_name
  cognitive_account_id = azurerm_cognitive_account.openai.id

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
  cognitive_account_id = azurerm_cognitive_account.openai.id

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
