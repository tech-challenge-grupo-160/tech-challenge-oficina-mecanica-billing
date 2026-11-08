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
src/
  API/              controllers, bootstrap, filtros, ProblemDetails
  Application/      comandos, consultas e handlers (MediatR), validação
  Domain/           entidades e regras de negócio
  Infrastructure/   persistência, mensageria, health checks, logs
  Shared/           código compartilhado entre camadas (LogTemplate)
tests/
  Fiap.TechChallenge.OficinaMecanica.Billing.Test.UnitTests/
  Fiap.TechChallenge.OficinaMecanica.Billing.Test.IntegrationTests/
infra/              Terraform SNS/SQS, DLQs e configuração no Secrets Manager
docker/backend/     Dockerfile (desenvolvimento, build e runtime com o tracer)
k8s/                Deployment, Service, HPA e ConfigMap (Kustomize)
```

A infraestrutura de mensageria e os passos de provisionamento estão em
[infra/README.md](infra/README.md). O catálogo comum está no
[repositório OS Service](https://github.com/tech-challenge-grupo-160/tech-challenge-oficina-mecanica/blob/develop/docs/mensageria/catalogo.md).

## Como rodar

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
