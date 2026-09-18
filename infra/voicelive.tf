# ─────────────────────────────────────────────
# OPTIONAL: VoiceLive realtime models
# Deployed on the SAME main Foundry account (azurerm_cognitive_account.foundry)
# so only one Foundry/AIServices resource exists in the subscription — not a
# dedicated account. Gated behind var.enable_voicelive so the rest of the
# stack can deploy without it.
# ─────────────────────────────────────────────

# ─────────────────────────────────────────────
# Realtime model deployment (gpt-realtime-mini)
# Required by the VoiceLive page
# ─────────────────────────────────────────────
resource "azurerm_cognitive_deployment" "voicelive_realtime" {
  count                = var.enable_voicelive ? 1 : 0
  name                 = var.voicelive_model_name
  cognitive_account_id = azurerm_cognitive_account.foundry.id

  model {
    format  = "OpenAI"
    name    = var.voicelive_model_name
    version = var.voicelive_model_version
  }

  sku {
    name     = "GlobalStandard"
    capacity = var.voicelive_model_capacity
  }
}

# ─────────────────────────────────────────────
# Realtime Pro model deployment (gpt-realtime)
# Selectable per session from the VoiceLive page.
# ─────────────────────────────────────────────
resource "azurerm_cognitive_deployment" "voicelive_realtime_pro" {
  count                = var.enable_voicelive ? 1 : 0
  name                 = var.voicelive_pro_model_name
  cognitive_account_id = azurerm_cognitive_account.foundry.id

  model {
    format  = "OpenAI"
    name    = var.voicelive_pro_model_name
    version = var.voicelive_pro_model_version
  }

  sku {
    name     = "GlobalStandard"
    capacity = var.voicelive_pro_model_capacity
  }
}
