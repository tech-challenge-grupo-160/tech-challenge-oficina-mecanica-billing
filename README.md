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

Depois da aprovação, a cobrança é feita no Mercado Pago (seção abaixo).

## Pagamento no Mercado Pago (F4-20)

O pagamento usa o **Checkout Pro**, em ambiente de teste.

1. Ao consumir `SolicitarPagamentoMessage`, o Billing grava o `Pagamento` e cria
   uma preferência no Mercado Pago com o valor do orçamento aprovado e
   `external_reference = os:<ordemServicoId>;pagamento:<pagamentoId>`. O link
   (`init_point`) fica em `linkPagamento` no
   `GET /api/v1/pagamentos/ordem-servico/{ordemServicoId}`.
2. O Mercado Pago avisa em `POST /api/v1/pagamentos/webhook` (tópico `payment`).
   O Billing valida o `x-signature` com a assinatura secreta do webhook
   (HMAC-SHA256 sobre `id:<data.id>;request-id:<x-request-id>;ts:<ts>;`) e
   responde `401` se ela não bater.
3. O status **nunca** vem do corpo da notificação: o Billing consulta
   `GET /v1/payments/{id}` na API, localiza o pagamento pela referência externa e
   confere o valor.
4. `approved` grava `Confirmado` e publica `PagamentoConfirmadoMessage`;
   `cancelled` (pagamento cancelado ou expirado) grava `Recusado` e publica
   `PagamentoRecusadoMessage`. `rejected` é só uma **tentativa** recusada: no
   Checkout Pro o comprador pode tentar de novo na mesma página, e cada tentativa
   é um pagamento novo no Mercado Pago. O pagamento continua `Pendente` e o
   desfecho é `Aguardando`. Status intermediários (`pending`, `in_process`...)
   também não mudam nada: o Mercado Pago avisa de novo quando o status mudar.
5. Notificações repetidas são idempotentes: o pagamento só sai de `Pendente` uma
   vez (escrita condicional no DynamoDB, junto com a Outbox), então o evento é
   publicado uma única vez e o webhook responde `200` com desfecho `Duplicada`.

### Credenciais

As credenciais ficam no AWS Secrets Manager, em `tc-grupo160/<ambiente>/mercadopago`
(JSON com `access_token` e `webhook_secret`), e nunca no repositório. No cluster o
pod recebe só o nome do segredo (`MercadoPago__SecretId`) e lê o valor no startup.
Sem o segredo preenchido o pod sobe mesmo assim, **sem cobrança**: os orçamentos
funcionam, o webhook responde `404` e o log da subida diz o motivo. Preencha o
segredo e reinicie os pods.

| Configuração | Uso |
|---|---|
| `MercadoPago__Enabled` | liga a integração (desligada, o pagamento segue pelo fluxo simulado) |
| `MercadoPago__SecretId` | nome do segredo no Secrets Manager (cluster) |
| `MercadoPago__AccessToken` / `MercadoPago__WebhookSecret` | só execução local, por `dotnet user-secrets` ou variável de ambiente |
| `MercadoPago__NotificationUrl` | opcional; vazio usa a URL configurada no painel do Mercado Pago |

Trocar o segredo exige reiniciar os pods:
`kubectl rollout restart deployment/oficina-mecanica-billing -n oficina-mecanica`.

A rota do webhook é pública no API Gateway (`POST /api/v1/pagamentos/webhook`, sem
JWT, criada na F4-14 no `tech-challenge-infra-k8s`), porque o Mercado Pago não manda
token do sistema. A regra do ALB para `/api/v1/pagamentos*` entrega a chamada ao
Billing. A proteção é a validação do `x-signature`. No painel do Mercado Pago, a
URL de notificação é `<gateway_url>/api/v1/pagamentos/webhook`.

Um pagamento recusado é final: uma aprovação que chegue depois da recusa é
ignorada e registrada em log, porque a saga já compensou a OS. O estorno (F4-21)
está fora do escopo (ADR-0004).

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

## Mercado Pago

O pagamento usa o **Checkout Pro**. As credenciais (access token e assinatura do
webhook) ficam no AWS Secrets Manager, em `tc-grupo160/<ambiente>/mercadopago`,
e nunca no repositório. Produto escolhido, contas de teste e o passo a passo para
gerar as credenciais estão em [docs/MERCADO-PAGO.md](docs/MERCADO-PAGO.md).

## Repositórios relacionados

| Serviço | Repositório |
|---|---|
| OS Service | [tech-challenge-oficina-mecanica](https://github.com/tech-challenge-grupo-160/tech-challenge-oficina-mecanica) |
| Billing | [tech-challenge-oficina-mecanica-billing](https://github.com/tech-challenge-grupo-160/tech-challenge-oficina-mecanica-billing) |
| Execução | [tech-challenge-oficina-mecanica-execucao](https://github.com/tech-challenge-grupo-160/tech-challenge-oficina-mecanica-execucao) |

Fluxo de branches: `feature/*` → `develop` → `homolog` → `main`.
