# Tabela unica do Billing no DynamoDB (F4-13, ADR-0006).
#
# Fica neste diretorio, e nao num repositorio de infra, pela ADR-0006: cada
# servico provisiona o proprio banco. Por estar no mesmo state da mensageria,
# entra sozinha no sobe-tudo.sh (etapa 5), no derruba-tudo.sh e no workflow
# terraform-messaging.yml, sem passo novo em nenhum deles.
#
# O esquema segue o que o codigo usa: chave composta PK/SK em texto, com
# orcamento, pagamento, inbox e outbox convivendo na mesma tabela
# (src/Infrastructure/Persistence). Localmente, o DynamoDbTableInitializer cria
# a mesma chave no LocalStack; na nuvem CreateTableIfMissing fica false e quem
# cria e este arquivo.
#
# O nome leva o ambiente porque dev, hom e prod dividem a mesma conta do
# Learner Lab. O pod recebe o nome pelo configmap (k8s/configmap.yaml).

resource "aws_dynamodb_table" "billing" {
  name = "${local.prefix}-billing"

  # Sob demanda: sem capacidade provisionada, nao cobra nada com o ambiente
  # parado e nao exige ajuste de leitura e escrita.
  billing_mode = "PAY_PER_REQUEST"

  hash_key  = "PK"
  range_key = "SK"

  attribute {
    name = "PK"
    type = "S"
  }

  attribute {
    name = "SK"
    type = "S"
  }

  attribute {
    name = "GSI1PK"
    type = "S"
  }

  # GSI1 do modelo da ADR-0006: localizar o pagamento so pelo id do Mercado
  # Pago. E esparso - so entram itens que gravarem GSI1PK - e hoje nenhum grava,
  # porque o webhook acha o pagamento pela external_reference (OS + pagamento).
  # Existe para o acesso previsto na ADR sem exigir recriar a tabela depois.
  global_secondary_index {
    name            = "GSI1"
    hash_key        = "GSI1PK"
    projection_type = "ALL"
  }

  # Expiracao de deduplicacao de webhook e de mensagens processadas (inbox).
  # O atributo e um epoch em segundos; item sem ele nunca expira.
  ttl {
    attribute_name = "ExpiraEmTtl"
    enabled        = true
  }

  # Backup continuo so onde ha dado a proteger. Em dev e hom o ambiente e
  # recriado a cada sessao do lab e o PITR so somaria custo.
  point_in_time_recovery {
    enabled = var.environment == "prod"
  }

  # Mesmo motivo do skip_final_snapshot dos RDS: o derruba-tudo.sh precisa
  # conseguir apagar a tabela.
  deletion_protection_enabled = false
}
