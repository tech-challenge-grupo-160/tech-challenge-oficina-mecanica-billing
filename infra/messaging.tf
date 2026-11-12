data "aws_caller_identity" "current" {}

data "aws_partition" "current" {}

locals {
  service = "billing"
  prefix  = "${var.project}-${var.environment}"

  topic_names = {
    os       = "${local.prefix}-os-events"
    billing  = "${local.prefix}-billing-events"
    execucao = "${local.prefix}-execucao-events"
  }

  topic_arns = {
    for service, name in local.topic_names :
    service => "arn:${data.aws_partition.current.partition}:sns:${var.region}:${data.aws_caller_identity.current.account_id}:${name}"
  }

  # Uma fila por comando, cada uma com DLQ propria (adendo de 11/11 a
  # ADR-0005). E a topologia que o codigo le e que o LocalStack monta
  # (docker/localstack do OS Service): cada consumidor tem sua fila, sem
  # roteamento pelo campo type. config_key e o nome que o codigo le em
  # Messaging__<config_key>.
  #
  # Comandos chegam por envio direto do OS Service (SendMessage), sem topico:
  # producer = null, entao nao ha assinatura nem queue policy para o SNS. O OS
  # Service monta a URL destas filas pelo nome (infra/messaging.tf de la).
  queues = {
    gerar_orcamento = {
      name       = "${local.prefix}-billing-gerar-orcamento"
      config_key = "GerarOrcamentoQueueUrl"
      producer   = null
      tipo       = null
    }
    solicitar_pagamento = {
      name       = "${local.prefix}-billing-solicitar-pagamento"
      config_key = "SolicitarPagamentoQueueUrl"
      producer   = null
      tipo       = null
    }
  }
}

# Topico dos eventos do Billing (OrcamentoGerado, OrcamentoAprovado,
# OrcamentoRecusado, PagamentoConfirmado, PagamentoRecusado). O publicador da
# Outbox grava o tipo no atributo "tipo"; quem filtra sao as filas do OS.
resource "aws_sns_topic" "events" {
  name              = local.topic_names[local.service]
  kms_master_key_id = "alias/aws/sns"
}

resource "aws_sqs_queue" "dlq" {
  for_each = local.queues

  name                      = "${each.value.name}-dlq"
  message_retention_seconds = var.dlq_retention_seconds
  sqs_managed_sse_enabled   = true
}

resource "aws_sqs_queue" "queue" {
  for_each = local.queues

  name                       = each.value.name
  message_retention_seconds  = var.queue_retention_seconds
  visibility_timeout_seconds = 60
  sqs_managed_sse_enabled    = true
  redrive_policy = jsonencode({
    deadLetterTargetArn = aws_sqs_queue.dlq[each.key].arn
    maxReceiveCount     = var.max_receive_count
  })
}

# Vazios aqui (o Billing so consome comandos), mas mantidos com o mesmo
# endereco dos outros servicos: o sobe-tudo.sh e o derruba-tudo.sh usam
# -target=aws_sns_topic_subscription.events nos tres repositorios.
resource "aws_sqs_queue_policy" "event_delivery" {
  for_each = {
    for key, queue in local.queues : key => queue
    if queue.producer != null
  }

  queue_url = aws_sqs_queue.queue[each.key].url
  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Sid       = "AllowDeliveryFromProducerTopic"
      Effect    = "Allow"
      Principal = { Service = "sns.amazonaws.com" }
      Action    = "sqs:SendMessage"
      Resource  = aws_sqs_queue.queue[each.key].arn
      Condition = {
        ArnEquals = {
          "aws:SourceArn" = local.topic_arns[each.value.producer]
        }
      }
    }]
  })
}

resource "aws_sns_topic_subscription" "events" {
  for_each = {
    for key, queue in local.queues : key => queue
    if queue.producer != null
  }

  topic_arn            = local.topic_arns[each.value.producer]
  protocol             = "sqs"
  endpoint             = aws_sqs_queue.queue[each.key].arn
  raw_message_delivery = true
  filter_policy_scope  = "MessageAttributes"
  filter_policy = jsonencode({
    tipo = [each.value.tipo]
  })

  depends_on = [aws_sqs_queue_policy.event_delivery]
}

resource "aws_secretsmanager_secret" "messaging_config" {
  name                    = "${var.project}/${var.environment}/billing/messaging"
  description             = "Topico, filas e configuracao de mensageria do Billing."
  recovery_window_in_days = 0
}

# As chaves sao exatamente as que o codigo le (Messaging__*), para o deploy
# copiar o segredo para o pod sem traducao. Messaging__Enabled liga os workers:
# so existe quando a infra existe.
resource "aws_secretsmanager_secret_version" "messaging_config" {
  secret_id = aws_secretsmanager_secret.messaging_config.id
  secret_string = jsonencode(merge(
    {
      Messaging__Enabled         = "true"
      Messaging__EventosTopicArn = aws_sns_topic.events.arn
    },
    {
      for key, queue in local.queues :
      "Messaging__${queue.config_key}" => aws_sqs_queue.queue[key].url
    }
  ))
}
