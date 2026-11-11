# Changelog

Todas as mudancas notaveis do servico serao documentadas neste arquivo.

O formato segue [Keep a Changelog](https://keepachangelog.com/pt-BR/1.1.0/).

## [Unreleased]

### Changed
- Campo `status` do log de requisicao HTTP renomeado para `statusHttp`, para o Datadog nao ler o codigo HTTP como nivel do log (F4-32)
- CI/CD pelo pipeline reutilizavel `servico-dotnet.yml@v1` do `tech-challenge-pipelines`: build, testes com cobertura e imagem em todo PR; deploy no EKS no push da `homolog` e da `main`, dev pelo disparo manual (F4-08)

### Added
- Cobranca no Mercado Pago (Checkout Pro) com referencia a OS e webhook `POST /api/v1/pagamentos/webhook`: valida o `x-signature`, consulta o pagamento na API antes de decidir, publica `PagamentoConfirmadoMessage` ou `PagamentoRecusadoMessage` pela Outbox e trata notificacoes repetidas de forma idempotente. Credenciais lidas do Secrets Manager no startup (F4-20)
- Documentacao do Mercado Pago: Checkout Pro como produto de pagamento, contas de teste, segredo por ambiente no Secrets Manager e passo a passo para gerar as credenciais (F4-06)
- Logs estruturados para seguir uma OS entre os servicos (F4-32): todo log JSON sai com `correlationId`, `ordemServicoId`, `trace_id`/`span_id` e bloco `dd` (`dd.trace_id`, `dd.span_id`) para o Datadog ligar log e trace; escopo aberto nas requisicoes HTTP, nos consumidores SQS e na Outbox; `DD_LOGS_INJECTION=true` na imagem; campos `level` e `message` reconhecidos pelo Datadog; mascara de CNPJ alem do CPF
- Pod com `tags.datadoghq.com/service` e `tags.datadoghq.com/env` e anotacao de logs `source: csharp`, para logs e traces do servico se ligarem no Datadog (F4-32)
- Esqueleto do servico: Clean Architecture, MediatR com validacao, health checks, logs em JSON, Dockerfile com o Datadog Tracer, manifests Kubernetes e CI de build e testes (F4-07)
- Terraform por serviço para o tópico SNS do Billing, filas de eventos/comandos, DLQs, filtros, criptografia, retenção e configuração no Secrets Manager (F4-11)
- Workflow independente para validar, planejar e aplicar a infraestrutura de mensageria por ambiente
