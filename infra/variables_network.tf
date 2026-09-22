variable "vnet_address_space" {
  description = "Espacio de direcciones de la VNet de la demo."
  type        = string
  default     = "10.20.0.0/16"
}

variable "subnet_pe_prefix" {
  description = "Subred donde viven los private endpoints."
  type        = string
  default     = "10.20.1.0/24"
}

variable "subnet_app_prefix" {
  description = "Subred delegada a App Service para la integracion VNet."
  type        = string
  default     = "10.20.2.0/24"
}

# El plano de datos de Storage (crear contenedores) no pasa por el private
# endpoint cuando Terraform corre fuera de la VNet. Esta es la puerta de
# administracion: se abre para desplegar y se cierra despues.
variable "admin_ip_address" {
  description = "IP publica que puede administrar el Storage. Vacio = ninguna."
  type        = string
  default     = ""
}

# ───────────────────────────────────────────── jumpbox + Bastion

variable "enable_jumpbox" {
  description = "Despliega VM jumpbox + Azure Bastion para administrar la red privada."
  type        = bool
  default     = true
}

variable "subnet_bastion_prefix" {
  description = "Subred AzureBastionSubnet (nombre fijo, minimo /26)."
  type        = string
  default     = "10.20.3.0/26"
}

variable "subnet_jumpbox_prefix" {
  description = "Subred de la NIC del jumpbox."
  type        = string
  default     = "10.20.4.0/27"
}

variable "bastion_sku" {
  description = "SKU de Azure Bastion. Basic alcanza para conectar desde el Portal."
  type        = string
  default     = "Basic"
}

variable "jumpbox_vm_size" {
  description = "Tamano de la VM jumpbox."
  type        = string
  # Standard_B2s no tiene capacidad en westus2 (verificado, SkuNotAvailable);
  # Dsv7 si tiene cuota y stock en la region.
  default     = "Standard_D2s_v7"
}

variable "jumpbox_admin_username" {
  description = "Usuario administrador local del jumpbox."
  type        = string
  default     = "azureadmin"
}
