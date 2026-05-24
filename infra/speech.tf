# ─────────────────────────────────────────────
# Azure Speech Service (optional)
# Used for LiveAvatar page (STT + TTS + animated avatar)
# kind = SpeechServices
# ─────────────────────────────────────────────
resource "azurerm_cognitive_account" "speech" {
  count                         = var.enable_speech ? 1 : 0
  name                          = "spch-${local.unique_name}"
  resource_group_name           = azurerm_resource_group.main.name
  location                      = var.speech_location
  kind                          = "SpeechServices"
  sku_name                      = var.speech_sku
  custom_subdomain_name         = "spch-${local.unique_name}"
  public_network_access_enabled = true
  local_auth_enabled            = true

  lifecycle {
    ignore_changes = [local_auth_enabled]
  }

  tags = local.common_tags
}
