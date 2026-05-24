# ─────────────────────────────────────────────
# Core
# ─────────────────────────────────────────────
variable "subscription_id" {
  description = "Azure Subscription ID"
  type        = string
}

variable "location" {
  description = "Azure region for all resources"
  type        = string
  default     = "eastus2"
}

variable "environment_name" {
  description = "Name of the environment (dev, staging, prod). Used as suffix for resource names."
  type        = string
  default     = "dev"
  validation {
    condition     = can(regex("^[a-z0-9]{1,10}$", var.environment_name))
    error_message = "environment_name must be 1-10 lowercase alphanumeric characters."
  }
}

variable "project_name" {
  description = "Short project prefix used in resource naming (e.g. 'afweb')"
  type        = string
  default     = "afweb"
  validation {
    condition     = can(regex("^[a-z0-9]{2,10}$", var.project_name))
    error_message = "project_name must be 2-10 lowercase alphanumeric characters."
  }
}

variable "tags" {
  description = "Tags to apply to all resources"
  type        = map(string)
  default = {
    project   = "AF-WebChat"
    framework = "Microsoft-Agent-Framework"
    iac       = "terraform"
  }
}

# ─────────────────────────────────────────────
# App Service
# ─────────────────────────────────────────────
# ─────────────────────────────────────────────
# App Service
# Web App SKU: B1 Linux works in westus2 (other regions blocked by 0
# "Total Regional VMs" quota in this MCAPS subscription).
# ─────────────────────────────────────────────
variable "app_service_sku" {
  description = "App Service Plan SKU name (F1 free, B1 basic, S1 standard, P1v3, etc.)"
  type        = string
  default     = "B1"
}

variable "app_service_location" {
  description = "Region for App Service Plan + Web App (must have App Service VM quota; only westus2 works in this MCAPS subscription)."
  type        = string
  default     = "westus2"
}

# ─────────────────────────────────────────────
# Azure OpenAI
# ─────────────────────────────────────────────
variable "openai_sku" {
  description = "SKU for Azure OpenAI account"
  type        = string
  default     = "S0"
}

variable "openai_chat_model_name" {
  description = "OpenAI chat model name to deploy"
  type        = string
  default     = "gpt-4o"
}

variable "openai_chat_model_version" {
  description = "Version of the chat model"
  type        = string
  default     = "2024-11-20"
}

variable "openai_chat_capacity" {
  description = "TPM capacity for chat model (in thousands)"
  type        = number
  default     = 30
}

variable "openai_embedding_model_name" {
  description = "OpenAI embedding model name to deploy"
  type        = string
  default     = "text-embedding-3-large"
}

variable "openai_embedding_model_version" {
  description = "Version of the embedding model"
  type        = string
  default     = "1"
}

variable "openai_embedding_capacity" {
  description = "TPM capacity for embedding model (in thousands)"
  type        = number
  default     = 30
}

# ─────────────────────────────────────────────
# Optional Features
# ─────────────────────────────────────────────
variable "enable_ai_search" {
  description = "Deploy Azure AI Search service (for RAG functionality)"
  type        = bool
  default     = true
}

variable "ai_search_sku" {
  description = "SKU for Azure AI Search (free, basic, standard)"
  type        = string
  default     = "basic"
}

variable "ai_search_location" {
  description = "Region for Azure AI Search (use a region with capacity; eastus2 is often exhausted)"
  type        = string
  default     = "eastus"
}

variable "enable_cosmos_db" {
  description = "Deploy Azure Cosmos DB (for persistent chat sessions)"
  type        = bool
  default     = true
}

variable "enable_sql_database" {
  description = "Deploy Azure SQL Database (for SQL agent queries)"
  type        = bool
  default     = true
}

variable "sql_admin_login" {
  description = "Admin login for SQL Server"
  type        = string
  default     = "sqladmin"
  sensitive   = true
}

variable "sql_admin_password" {
  description = "Admin password for SQL Server (required if enable_sql_database = true). Set via terraform.tfvars or TF_VAR_sql_admin_password."
  type        = string
  default     = ""
  sensitive   = true
}

variable "sql_database_name" {
  description = "Name of the SQL database to create"
  type        = string
  default     = "sqldb-afwebchat"
}

variable "sql_database_sku" {
  description = "SQL Database SKU (Basic, S0, S1, GP_S_Gen5_1 for serverless, etc.)"
  type        = string
  default     = "Basic"
}

variable "enable_bing_search" {
  description = "Deploy Bing Search resource (NOTE: Bing Search APIs were retired; new deployments fail and this is kept disabled)."
  type        = bool
  default     = false
}

variable "enable_bot_service" {
  description = "Deploy Azure Bot Service (for Teams channel integration)"
  type        = bool
  default     = true
}

variable "bot_sku" {
  description = "Azure Bot Service SKU (F0 free, S1 standard)"
  type        = string
  default     = "F0"
}

# ─────────────────────────────────────────────
# Speech Service (optional, for LiveAvatar)
# ─────────────────────────────────────────────
variable "enable_speech" {
  description = "Deploy Azure Speech Service (STT, TTS, talking avatar)"
  type        = bool
  default     = true
}

variable "speech_sku" {
  description = "SKU for Speech Service (F0 free, S0 standard)"
  type        = string
  default     = "S0"
}

variable "speech_location" {
  description = "Region for Speech Service (avatar requires regions like eastus2, westus2, etc.)"
  type        = string
  default     = "eastus2"
}

# ─────────────────────────────────────────────
# AI Services account (multi-service, for VoiceLive realtime)
# ─────────────────────────────────────────────
variable "enable_ai_services" {
  description = "Deploy Azure AI Services account with a realtime model deployment for the VoiceLive page"
  type        = bool
  default     = true
}

variable "ai_services_sku" {
  description = "SKU for AI Services account"
  type        = string
  default     = "S0"
}

variable "ai_services_location" {
  description = "Region for AI Services (must support gpt-4o realtime models, e.g. eastus2)"
  type        = string
  default     = "eastus2"
}

variable "voicelive_model_name" {
  description = "Realtime model name to deploy for VoiceLive"
  type        = string
  default     = "gpt-realtime-mini"
}

variable "voicelive_model_version" {
  description = "Realtime model version"
  type        = string
  default     = "2025-10-06"
}

variable "voicelive_model_capacity" {
  description = "Realtime model TPM capacity (in thousands). GlobalStandard SKU."
  type        = number
  default     = 1
}
