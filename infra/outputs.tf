output "event_topic_name" {
  description = "Nome do topico SNS publicado pelo Billing."
  value       = aws_sns_topic.events.name
}

output "event_topic_arn" {
  description = "ARN do topico SNS publicado pelo Billing."
  value       = aws_sns_topic.events.arn
}

output "queue_names" {
  description = "Nomes das filas que o Billing consome."
  value       = { for key, queue in aws_sqs_queue.queue : key => queue.name }
}

output "queue_arns" {
  description = "ARNs das filas que o Billing consome."
  value       = { for key, queue in aws_sqs_queue.queue : key => queue.arn }
}

output "queue_urls" {
  description = "URLs das filas que o Billing consome."
  value       = { for key, queue in aws_sqs_queue.queue : key => queue.url }
}

output "messaging_config_secret_name" {
  description = "Nome do segredo JSON com os nomes, URLs e ARNs consumidos pelo servico."
  value       = aws_secretsmanager_secret.messaging_config.name
}

output "messaging_config_secret_arn" {
  description = "ARN do segredo de configuracao de mensageria."
  value       = aws_secretsmanager_secret.messaging_config.arn
}

output "dynamodb_table_name" {
  description = "Nome da tabela unica do Billing (F4-13). O pod recebe pelo configmap."
  value       = aws_dynamodb_table.billing.name
}

output "dynamodb_table_arn" {
  description = "ARN da tabela unica do Billing."
  value       = aws_dynamodb_table.billing.arn
}
