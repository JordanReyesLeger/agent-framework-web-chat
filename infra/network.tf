// Red privada de la demo: VNet, subredes, zonas DNS privadas, private endpoints
// y los shared private links que necesita el indexer de AI Search para seguir
// funcionando cuando Storage y Foundry dejan de ser publicos.

locals {
  pe_name = "pe-${local.unique_name}"

  // Un recurso kind=AIServices publica los tres FQDN: hay que resolver los tres
  // o el SDK falla segun por cual entre.
  foundry_dns_zones = [
    "privatelink.cognitiveservices.azure.com",
    "privatelink.openai.azure.com",
    "privatelink.services.ai.azure.com",
  ]
}

# ───────────────────────────────────────────── red

resource "azurerm_virtual_network" "main" {
  name                = "vnet-${local.unique_name}"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  address_space       = [var.vnet_address_space]
  tags                = local.common_tags
}

resource "azurerm_subnet" "private_endpoints" {
  name                 = "snet-pe"
  resource_group_name  = azurerm_resource_group.main.name
  virtual_network_name = azurerm_virtual_network.main.name
  address_prefixes     = [var.subnet_pe_prefix]

  private_endpoint_network_policies = "Disabled"
}

# La integracion VNet del App Service exige una subred delegada y vacia.
resource "azurerm_subnet" "app" {
  name                 = "snet-app"
  resource_group_name  = azurerm_resource_group.main.name
  virtual_network_name = azurerm_virtual_network.main.name
  address_prefixes     = [var.subnet_app_prefix]

  delegation {
    name = "appservice"
    service_delegation {
      name    = "Microsoft.Web/serverFarms"
      actions = ["Microsoft.Network/virtualNetworks/subnets/action"]
    }
  }
}

# ───────────────────────────────────────────── zonas DNS privadas

locals {
  dns_zones = toset(concat(
    local.foundry_dns_zones,
    ["privatelink.blob.core.windows.net"],
    var.enable_ai_search ? ["privatelink.search.windows.net"] : [],
    var.enable_cosmos_db ? ["privatelink.documents.azure.com"] : [],
  ))
}

resource "azurerm_private_dns_zone" "main" {
  for_each            = local.dns_zones
  name                = each.value
  resource_group_name = azurerm_resource_group.main.name
  tags                = local.common_tags
}

# Sin este vinculo el private endpoint existe pero nadie lo resuelve: la app
# sigue yendo a la IP publica y el sintoma es un timeout sin mensaje util.
resource "azurerm_private_dns_zone_virtual_network_link" "main" {
  for_each             = azurerm_private_dns_zone.main
  name                 = "link-${replace(each.key, ".", "-")}"
  private_dns_zone_id  = each.value.id
  virtual_network_id   = azurerm_virtual_network.main.id
  registration_enabled = false
  tags                 = local.common_tags
}

# ───────────────────────────────────────────── private endpoints

resource "azurerm_private_endpoint" "foundry" {
  name                = "${local.pe_name}-foundry"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  subnet_id           = azurerm_subnet.private_endpoints.id
  tags                = local.common_tags

  private_service_connection {
    name                           = "psc-foundry"
    private_connection_resource_id = azurerm_cognitive_account.foundry.id
    subresource_names              = ["account"]
    is_manual_connection           = false
  }

  private_dns_zone_group {
    name                 = "foundry-zones"
    private_dns_zone_ids = [for z in local.foundry_dns_zones : azurerm_private_dns_zone.main[z].id]
  }
}

resource "azurerm_private_endpoint" "storage_blob" {
  name                = "${local.pe_name}-blob"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  subnet_id           = azurerm_subnet.private_endpoints.id
  tags                = local.common_tags

  private_service_connection {
    name                           = "psc-blob"
    private_connection_resource_id = azurerm_storage_account.main.id
    subresource_names              = ["blob"]
    is_manual_connection           = false
  }

  private_dns_zone_group {
    name                 = "blob-zone"
    private_dns_zone_ids = [azurerm_private_dns_zone.main["privatelink.blob.core.windows.net"].id]
  }
}

resource "azurerm_private_endpoint" "search" {
  count               = var.enable_ai_search ? 1 : 0
  name                = "${local.pe_name}-search"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  subnet_id           = azurerm_subnet.private_endpoints.id
  tags                = local.common_tags

  private_service_connection {
    name                           = "psc-search"
    private_connection_resource_id = azurerm_search_service.main[0].id
    subresource_names              = ["searchService"]
    is_manual_connection           = false
  }

  private_dns_zone_group {
    name                 = "search-zone"
    private_dns_zone_ids = [azurerm_private_dns_zone.main["privatelink.search.windows.net"].id]
  }
}

resource "azurerm_private_endpoint" "cosmos" {
  count               = var.enable_cosmos_db ? 1 : 0
  name                = "${local.pe_name}-cosmos"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  subnet_id           = azurerm_subnet.private_endpoints.id
  tags                = local.common_tags

  private_service_connection {
    name                           = "psc-cosmos"
    private_connection_resource_id = azurerm_cosmosdb_account.main[0].id
    subresource_names              = ["Sql"] # case-sensitive
    is_manual_connection           = false
  }

  private_dns_zone_group {
    name                 = "cosmos-zone"
    private_dns_zone_ids = [azurerm_private_dns_zone.main["privatelink.documents.azure.com"].id]
  }
}

# ───────────────────────────────────────────── shared private links del indexer

# El indexer de AI Search NO corre dentro de la VNet: sale del runtime del
# servicio. Con Storage y Foundry cerrados, la unica via es un shared private
# link por cada destino. Sin esto la subida de documentos se indexa "para
# siempre" sin error visible en la app.
# Quedan en estado Pending: hay que aprobarlos en el recurso destino.

resource "azurerm_search_shared_private_link_service" "storage_blob" {
  count              = var.enable_ai_search ? 1 : 0
  name               = "spl-blob"
  search_service_id  = azurerm_search_service.main[0].id
  subresource_name   = "blob"
  target_resource_id = azurerm_storage_account.main.id
  request_message    = "Indexer de AI Search hacia el storage de documentos"
}

resource "azurerm_search_shared_private_link_service" "foundry_openai" {
  count              = var.enable_ai_search ? 1 : 0
  name               = "spl-openai"
  search_service_id  = azurerm_search_service.main[0].id
  subresource_name   = "openai_account"
  target_resource_id = azurerm_cognitive_account.foundry.id
  request_message    = "Skill de embeddings hacia Foundry"
}

resource "azurerm_search_shared_private_link_service" "foundry_cognitive" {
  count              = var.enable_ai_search ? 1 : 0
  name               = "spl-cognitive"
  search_service_id  = azurerm_search_service.main[0].id
  subresource_name   = "cognitiveservices_account"
  target_resource_id = azurerm_cognitive_account.foundry.id
  request_message    = "Skills OCR/merge hacia Foundry (facturacion keyless)"
}
