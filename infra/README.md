# Mensageria do Billing

Este diretorio provisiona o topico SNS publicado pelo Billing, uma fila SQS por
comando que ele consome, uma DLQ por fila e a configuracao no AWS Secrets
Manager. E a mesma topologia do LocalStack (`docker/localstack/init-aws.sh` do
OS Service), descrita no adendo de 11/11 a ADR-0005: uma fila por tipo de
mensagem, sem roteamento pelo campo `type`.

| Fila | Comando | Chave que o codigo le |
|---|---|---|
| `...-billing-gerar-orcamento` | `GerarOrcamentoMessage` | `GerarOrcamentoQueueUrl` |
| `...-billing-solicitar-pagamento` | `SolicitarPagamentoMessage` | `SolicitarPagamentoQueueUrl` |

Os comandos chegam por envio direto do OS Service (`SendMessage`), sem topico
nem assinatura. O topico `tc-grupo160-<ambiente>-billing-events` recebe os eventos
(`OrcamentoGerado`, `OrcamentoAprovado`, `OrcamentoRecusado`,
`PagamentoConfirmado`, `PagamentoRecusado`); o publicador da Outbox grava o tipo no atributo `tipo`
e quem filtra sao as filas do OS Service.

O topico usa a chave gerenciada pela AWS para SNS. Filas e DLQs usam
criptografia gerenciada pelo SQS, retencao de 4 e 14 dias e redrive apos 5
recebimentos.

O segredo `tc-grupo160/<ambiente>/billing/messaging` tem exatamente as chaves
`Messaging__*` que o codigo le, inclusive `Messaging__Enabled`. O workflow de
deploy as injeta pelo parametro `segredos` e o `sobe-tudo.sh` copia todas para
o Secret do pod. O catalogo esta em
[docs/mensageria/catalogo.md](https://github.com/tech-challenge-grupo-160/tech-challenge-oficina-mecanica/blob/develop/docs/mensageria/catalogo.md).

## Tabela DynamoDB (F4-13)

O mesmo state cria a tabela única do Billing, `tc-grupo160-<ambiente>-billing`
([`dynamodb.tf`](dynamodb.tf)), pelo modelo da
[ADR-0006](https://github.com/tech-challenge-grupo-160/tech-challenge-oficina-mecanica/blob/develop/docs/adrs/0006-banco-por-servico.md):

| Item | Valor |
|---|---|
| Chave | `PK` (partição) e `SK` (ordenação), texto, a mesma que o código usa |
| Cobrança | sob demanda: nada com o ambiente parado |
| Índice | `GSI1` por `GSI1PK`, esparso: pagamento pelo id do Mercado Pago. Hoje nenhum item grava `GSI1PK` |
| TTL | atributo `ExpiraEmTtl`, epoch em segundos, para deduplicação de webhook e inbox |
| Point-in-time recovery | só em `prod` |
| Proteção contra exclusão | desligada, para o `derruba-tudo.sh` conseguir apagar |

O pod recebe o nome pelo `k8s/configmap.yaml` (`DynamoDb__TableName`, com
`__AMBIENTE__` trocado no deploy) e acessa a tabela pela `LabRole` dos nodes
(F4-05). Localmente, o `DynamoDbTableInitializer` cria a mesma chave no
LocalStack.

Por estar neste diretório, a tabela sobe e desce junto com a mensageria: etapa
5 do `sobe-tudo.sh`, passo 2 do `derruba-tudo.sh` e o workflow
`terraform-messaging.yml`. **O destroy apaga os dados**, sem backup fora de `prod`.

## Aplicacao

```powershell
terraform init -reconfigure `
  -backend-config="bucket=tc-grupo160-tfstate-<id-da-conta>" `
  -backend-config="key=dev/messaging-billing.tfstate" `
  -backend-config="region=us-east-1" `
  -backend-config="dynamodb_table=tc-grupo160-tflock"
terraform plan -var="environment=dev"
```

No primeiro provisionamento, crie primeiro somente `aws_sns_topic.events` em
cada servico. Com os tres topicos presentes, aplique o Terraform completo nos
consumidores para criar filas e assinaturas. Use a mesma conta, regiao,
ambiente e prefixo nos tres repositorios. O workflow `terraform-messaging.yml`
planeja PRs e aplica em push nas branches de ambiente; sem push, este commit
não dispara apply.

O roteiro PowerShell de provisionamento e validação no AWS Academy, incluindo
filtros, redrive, criptografia e limpeza/ciclo de vida, está no
[catálogo de mensageria](https://github.com/tech-challenge-grupo-160/tech-challenge-oficina-mecanica/blob/develop/docs/mensageria/catalogo.md#teste-direto-na-aws-academy-learner-lab).
O acesso de pods EKS ao SNS/SQS não é validado apenas por criar recursos e
ainda requer o spike F4-05.
