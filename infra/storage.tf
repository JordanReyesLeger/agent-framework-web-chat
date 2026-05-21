# ─────────────────────────────────────────────
# Azure Storage Account
# ─────────────────────────────────────────────
resource "azurerm_storage_account" "main" {
  name                     = local.storage_account_name
  resource_group_name      = azurerm_resource_group.main.name
  location                 = azurerm_resource_group.main.location
  account_tier             = "Standard"
  account_replication_type = "LRS"
  min_tls_version          = "TLS1_2"

  blob_properties {
    delete_retention_policy {
      days = 7
    }
  }

  tags = local.common_tags
}

# ─────────────────────────────────────────────
# Blob Containers
# ─────────────────────────────────────────────
resource "azurerm_storage_container" "documents" {
  name                  = "documents"
  storage_account_id    = azurerm_storage_account.main.id
  container_access_type = "private"
}

resource "azurerm_storage_container" "skill_documents" {
  name                  = "skill-documents"
  storage_account_id    = azurerm_storage_account.main.id
  container_access_type = "private"
}
