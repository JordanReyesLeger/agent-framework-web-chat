# ─────────────────────────────────────────────
# Data sources
# ─────────────────────────────────────────────
data "azurerm_client_config" "current" {}

# ─────────────────────────────────────────────
# Random suffix for globally unique names
# ─────────────────────────────────────────────
resource "random_string" "suffix" {
  length  = 5
  upper   = false
  special = false
}

locals {
  # Base name for resources: e.g. "afweb-dev-a1b2c"
  name_prefix = "${var.project_name}-${var.environment_name}"
  name_suffix = random_string.suffix.result
  unique_name = "${local.name_prefix}-${local.name_suffix}"

  # Storage accounts require no hyphens and max 24 chars
  storage_account_name = substr(replace("st${var.project_name}${var.environment_name}${local.name_suffix}", "-", ""), 0, 24)

  common_tags = merge(var.tags, {
    environment     = var.environment_name
    securityControl = "Ignore"
  })
}

# ─────────────────────────────────────────────
# Resource Group
# ─────────────────────────────────────────────
resource "azurerm_resource_group" "main" {
  name     = "rg-${local.unique_name}"
  location = var.location
  tags     = local.common_tags
}
