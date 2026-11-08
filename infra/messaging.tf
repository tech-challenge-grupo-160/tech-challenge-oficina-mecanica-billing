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

  queues = {
    os_events = {
      name     = "${local.prefix}-billing-os-eventos"
      producer = "os"
      message_types = [
        "os.aberta",
        "os.cancelada",
      ]
    }
    commands = {
      name          = "${local.prefix}-billing-comandos"
      producer      = null
      message_types = []
    }
  }
}

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
  filter_policy_scope  = "MessageBody"
  filter_policy = jsonencode({
    type = each.value.message_types
  })

  depends_on = [aws_sqs_queue_policy.event_delivery]
}

resource "aws_secretsmanager_secret" "messaging_config" {
  name                    = "${var.project}/${var.environment}/billing/messaging"
  description             = "Topico, filas e destinos de mensageria do Billing."
  recovery_window_in_days = 0
}

resource "aws_secretsmanager_secret_version" "messaging_config" {
  secret_id = aws_secretsmanager_secret.messaging_config.id
  secret_string = jsonencode({
    Messaging__EventTopicName    = aws_sns_topic.events.name
    Messaging__EventTopicArn     = aws_sns_topic.events.arn
    Messaging__OsEventsQueueName = aws_sqs_queue.queue["os_events"].name
    Messaging__OsEventsQueueUrl  = aws_sqs_queue.queue["os_events"].url
    Messaging__OsEventsQueueArn  = aws_sqs_queue.queue["os_events"].arn
    Messaging__OsEventsDlqArn    = aws_sqs_queue.dlq["os_events"].arn
    Messaging__CommandsQueueName = aws_sqs_queue.queue["commands"].name
    Messaging__CommandsQueueUrl  = aws_sqs_queue.queue["commands"].url
    Messaging__CommandsQueueArn  = aws_sqs_queue.queue["commands"].arn
    Messaging__CommandsDlqArn    = aws_sqs_queue.dlq["commands"].arn
  })
}
