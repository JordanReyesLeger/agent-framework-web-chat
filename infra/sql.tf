# ─────────────────────────────────────────────
# Azure SQL Server + Database (optional)
# Used by the SqlAzure / EmailData agents
# ─────────────────────────────────────────────
resource "azurerm_mssql_server" "main" {
  count                        = var.enable_sql_database ? 1 : 0
  name                         = "sql-${local.unique_name}"
  resource_group_name          = azurerm_resource_group.main.name
  location                     = azurerm_resource_group.main.location
  version                      = "12.0"
  administrator_login          = var.sql_admin_login
  administrator_login_password = var.sql_admin_password
  minimum_tls_version          = "1.2"

  azuread_administrator {
    login_username = data.azurerm_client_config.current.object_id
    object_id      = data.azurerm_client_config.current.object_id
    tenant_id      = data.azurerm_client_config.current.tenant_id
  }

  tags = local.common_tags
}

resource "azurerm_mssql_database" "main" {
  count        = var.enable_sql_database ? 1 : 0
  name         = var.sql_database_name
  server_id    = azurerm_mssql_server.main[0].id
  collation    = "SQL_Latin1_General_CP1_CI_AS"
  license_type = "LicenseIncluded"
  sku_name     = var.sql_database_sku
  max_size_gb  = 2

  tags = local.common_tags
}

# Allow Azure services (App Service, etc.) to reach the SQL server.
resource "azurerm_mssql_firewall_rule" "azure_services" {
  count            = var.enable_sql_database ? 1 : 0
  name             = "AllowAzureServices"
  server_id        = azurerm_mssql_server.main[0].id
  start_ip_address = "0.0.0.0"
  end_ip_address   = "0.0.0.0"
}
