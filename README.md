# Billing Service — Oficina Mecânica

[![Quality Gate](https://sonarcloud.io/api/project_badges/measure?project=tech-challenge-grupo-160_tech-challenge-oficina-mecanica-billing&metric=alert_status)](https://sonarcloud.io/summary/new_code?id=tech-challenge-grupo-160_tech-challenge-oficina-mecanica-billing) [![Cobertura](https://sonarcloud.io/api/project_badges/measure?project=tech-challenge-grupo-160_tech-challenge-oficina-mecanica-billing&metric=coverage)](https://sonarcloud.io/summary/overall?id=tech-challenge-grupo-160_tech-challenge-oficina-mecanica-billing)

Microsserviço de orçamento e pagamento da oficina mecânica, desenvolvido na Fase 4 do Tech Challenge FIAP SOAT pelo Grupo 160.

## Corte funcional atual

O Billing já executa o primeiro fluxo assíncrono de orçamento:

1. consome `GerarOrcamentoMessage` da fila SQS `billing-gerar-orcamento`;
2. calcula os totais usando os snapshots de serviços e peças enviados pelo OS Service;
3. grava `Orcamento`, `Inbox` e `Outbox` atomicamente no DynamoDB;
4. trata reprocessamentos de forma idempotente;
5. publica `OrcamentoGeradoMessage` no tópico SNS `oficina-eventos`;
6. disponibiliza a consulta do orçamento por ordem de serviço.

Aprovação/recusa, criação da cobrança e integração com Mercado Pago ficam nos próximos cortes.

## Contratos

### Comando consumido

`GerarOrcamentoMessage`, versão 1, enviado pelo OS Service após o recebimento
do diagnóstico concluído. O comando contém o parecer técnico e os snapshots
comerciais dos serviços e peças diagnosticados.

### Evento publicado

`OrcamentoGeradoMessage`, versão 1, contendo:

- identificadores da mensagem, correlação, orçamento, OS e diagnóstico;
- status do orçamento;
- valores de serviços, peças e total.

### Endpoint

```http
GET /api/v1/orcamentos/ordem-servico/{ordemServicoId}
```

Retorna o orçamento com parecer, itens, preços congelados e totais. Retorna `404` quando ainda não existe orçamento para a OS.

## Stack

.NET 10, ASP.NET Core, Clean Architecture, MediatR, FluentValidation, Amazon DynamoDB, SNS, SQS, Inbox/Outbox e logs estruturados em JSON.

## Como rodar localmente

O LocalStack compartilhado é iniciado pelo repositório do OS Service. Ele deve estar com SNS, SQS e DynamoDB habilitados e com a fila do Billing provisionada.

No OS Service:

```bash
docker compose up -d localstack
```

No Billing:

```bash
export LOCALSTACK_ACCESS_KEY_ID=<credencial-local>
export LOCALSTACK_SECRET_ACCESS_KEY=<credencial-local>
docker compose up -d --build
```

A API fica disponível em `http://localhost:8081` e o Swagger em `http://localhost:8081/swagger`.

## Testes

```bash
dotnet test Fiap.TechChallenge.OficinaMecanica.Billing.slnx \
  --collect:"XPlat Code Coverage" \
  --settings coverlet.runsettings
```

Os testes cobrem domínio, validações, handlers, idempotência e transação no DynamoDB, contratos de mensagens e endpoint HTTP. A meta do Quality Gate é cobertura mínima de 80% em código novo.

## Repositórios relacionados

| Serviço | Repositório |
|---|---|
| OS Service | [tech-challenge-oficina-mecanica](https://github.com/tech-challenge-grupo-160/tech-challenge-oficina-mecanica) |
| Billing | [tech-challenge-oficina-mecanica-billing](https://github.com/tech-challenge-grupo-160/tech-challenge-oficina-mecanica-billing) |
| Execução | [tech-challenge-oficina-mecanica-execucao](https://github.com/tech-challenge-grupo-160/tech-challenge-oficina-mecanica-execucao) |

Fluxo de branches: `feature/*` → `develop` → `homolog` → `main`.
