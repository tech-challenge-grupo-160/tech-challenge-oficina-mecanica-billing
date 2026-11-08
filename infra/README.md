# Mensageria do Billing

Este diretorio provisiona o topico SNS publicado pelo Billing, a fila de
eventos do OS Service, a fila direta de comandos, suas DLQs e a configuracao no
AWS Secrets Manager.

O topico `tc-grupo160-<ambiente>-billing-events` usa a chave gerenciada pela
AWS para SNS. Filas e DLQs usam criptografia gerenciada pelo SQS, retencao de 4
e 14 dias e redrive apos 5 recebimentos. A assinatura filtra os eventos
`os.aberta` e `os.cancelada` pelo campo `type` do envelope.

O segredo `tc-grupo160/<ambiente>/billing/messaging` contem os nomes, URLs e
ARNs do topico e das filas. O catalogo esta em
[docs/mensageria/catalogo.md](https://github.com/tech-challenge-grupo-160/tech-challenge-oficina-mecanica/blob/develop/docs/mensageria/catalogo.md).
Ao migrar o deploy para o workflow compartilhado, injete os campos
`Messaging__EventTopicArn`, `Messaging__OsEventsQueueUrl` e
`Messaging__CommandsQueueUrl` pelo parâmetro `segredos` do workflow, usando como
origem `tc-grupo160/{sufixo}/billing/messaging#<campo>`.

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
