variable "project" {
  description = "Prefixo compartilhado dos recursos AWS."
  type        = string
  default     = "tc-grupo160"
}

variable "environment" {
  description = "Ambiente logico."
  type        = string

  validation {
    condition     = contains(["dev", "hom", "prod"], var.environment)
    error_message = "environment deve ser dev, hom ou prod."
  }
}

variable "region" {
  description = "Regiao AWS."
  type        = string
  default     = "us-east-1"
}

variable "queue_retention_seconds" {
  description = "Retencao das filas de consumo em segundos."
  type        = number
  default     = 345600

  validation {
    condition     = var.queue_retention_seconds >= 60 && var.queue_retention_seconds <= 1209600
    error_message = "A retencao de uma fila SQS deve ficar entre 60 segundos e 14 dias."
  }
}

variable "dlq_retention_seconds" {
  description = "Retencao das DLQs em segundos."
  type        = number
  default     = 1209600

  validation {
    condition     = var.dlq_retention_seconds >= 60 && var.dlq_retention_seconds <= 1209600 && var.dlq_retention_seconds >= var.queue_retention_seconds
    error_message = "A retencao da DLQ deve ficar entre 60 segundos e 14 dias e nao pode ser menor que a fila de origem."
  }
}

variable "max_receive_count" {
  description = "Quantidade de entregas antes de encaminhar a mensagem para a DLQ."
  type        = number
  default     = 5

  validation {
    condition     = var.max_receive_count >= 1
    error_message = "max_receive_count deve ser maior que zero."
  }
}
