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
variable "app_service_sku" {
  description = "App Service Plan SKU name (B1, B2, S1, P1v2, P1v3, etc.)"
  type        = string
  default     = "B1"
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

variable "enable_cosmos_db" {
  description = "Deploy Azure Cosmos DB (for persistent chat sessions)"
  type        = bool
  default     = false
}

variable "enable_document_intelligence" {
  description = "Deploy Azure Document Intelligence (for OCR/document processing)"
  type        = bool
  default     = false
}

variable "enable_sql_database" {
  description = "Deploy Azure SQL Database (for SQL agent queries)"
  type        = bool
  default     = false
}

variable "sql_admin_login" {
  description = "Admin login for SQL Server (required if enable_sql_database = true)"
  type        = string
  default     = "sqladmin"
  sensitive   = true
}

variable "sql_admin_password" {
  description = "Admin password for SQL Server (required if enable_sql_database = true)"
  type        = string
  default     = ""
  sensitive   = true
}

variable "enable_bing_search" {
  description = "Deploy Bing Search resource (for web grounding)"
  type        = bool
  default     = false
}

variable "enable_bot_service" {
  description = "Deploy Azure Bot Service (for Teams channel integration)"
  type        = bool
  default     = false
}

variable "bot_app_client_id" {
  description = "Microsoft Entra App Registration Client ID for Bot Service (required if enable_bot_service = true)"
  type        = string
  default     = ""
}

variable "bot_app_client_secret" {
  description = "Microsoft Entra App Registration Client Secret for Bot Service (required if enable_bot_service = true)"
  type        = string
  default     = ""
  sensitive   = true
}
