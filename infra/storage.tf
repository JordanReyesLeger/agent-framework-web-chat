# ─────────────────────────────────────────────
# Azure Storage Account
# ─────────────────────────────────────────────
resource "azurerm_storage_account" "main" {
  name                            = local.storage_account_name
  resource_group_name             = azurerm_resource_group.main.name
  location                        = azurerm_resource_group.main.location
  account_tier                    = "Standard"
  account_replication_type        = "LRS"
  min_tls_version                 = "TLS1_2"
  shared_access_key_enabled       = false
  default_to_oauth_authentication = true

  # Terraform crea los contenedores por el plano de DATOS, que no pasa por el
  # private endpoint si se corre fuera de la VNet. Por eso el endpoint publico
  # queda en Deny con una sola excepcion: la IP de administracion. El trafico de
  # la app entra por el private endpoint.
  network_rules {
    default_action = "Deny"
    bypass         = ["AzureServices"]
    ip_rules       = var.admin_ip_address == "" ? [] : [var.admin_ip_address]
  }

  blob_properties {
    delete_retention_policy {
      days = 7
    }
  }

  lifecycle {
    ignore_changes = [allow_nested_items_to_be_public]
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
