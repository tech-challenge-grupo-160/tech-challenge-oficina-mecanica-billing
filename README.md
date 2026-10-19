# Billing Service — Oficina Mecânica

[![Quality Gate](https://sonarcloud.io/api/project_badges/measure?project=tech-challenge-grupo-160_tech-challenge-oficina-mecanica-billing&metric=alert_status)](https://sonarcloud.io/summary/new_code?id=tech-challenge-grupo-160_tech-challenge-oficina-mecanica-billing) [![Cobertura](https://sonarcloud.io/api/project_badges/measure?project=tech-challenge-grupo-160_tech-challenge-oficina-mecanica-billing&metric=coverage)](https://sonarcloud.io/summary/overall?id=tech-challenge-grupo-160_tech-challenge-oficina-mecanica-billing) [![Bugs](https://sonarcloud.io/api/project_badges/measure?project=tech-challenge-grupo-160_tech-challenge-oficina-mecanica-billing&metric=bugs)](https://sonarcloud.io/summary/overall?id=tech-challenge-grupo-160_tech-challenge-oficina-mecanica-billing) [![Vulnerabilidades](https://sonarcloud.io/api/project_badges/measure?project=tech-challenge-grupo-160_tech-challenge-oficina-mecanica-billing&metric=vulnerabilities)](https://sonarcloud.io/summary/overall?id=tech-challenge-grupo-160_tech-challenge-oficina-mecanica-billing)

Microsserviço da Fase 4 do Tech Challenge (FIAP SOAT, Grupo 160).

**Responsabilidade:** Orçamento (geração, envio, aprovação e recusa) e pagamento via Mercado Pago.

Faz parte da divisão definida na [ADR-0003](https://github.com/tech-challenge-grupo-160/tech-challenge-oficina-mecanica/blob/develop/docs/adrs/0003-divisao-em-microsservicos.md):

| Serviço | Repositório |
|---|---|
| OS Service | [tech-challenge-oficina-mecanica](https://github.com/tech-challenge-grupo-160/tech-challenge-oficina-mecanica) |
| Billing | [tech-challenge-oficina-mecanica-billing](https://github.com/tech-challenge-grupo-160/tech-challenge-oficina-mecanica-billing) |
| Execução | [tech-challenge-oficina-mecanica-execucao](https://github.com/tech-challenge-grupo-160/tech-challenge-oficina-mecanica-execucao) |

> **Estado:** esqueleto inicial ([F4-07](https://github.com/tech-challenge-grupo-160/tech-challenge-oficina-mecanica/issues/138)). O domínio, o banco e a mensageria entram nas próximas tasks do [board da Fase 4](https://github.com/orgs/tech-challenge-grupo-160/projects/5).

## Stack

.NET 10 · ASP.NET Core · Clean Architecture (API → Application → Domain → Infrastructure) · MediatR com `ValidationBehavior` · FluentValidation · logs em JSON · Datadog .NET Tracer na imagem.

Banco: Amazon DynamoDB (tabela única), provisionado pela [F4-13](https://github.com/tech-challenge-grupo-160/tech-challenge-oficina-mecanica/issues/144).

## Estrutura

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
docker/backend/     Dockerfile (desenvolvimento, build e runtime com o tracer)
k8s/                Deployment, Service, HPA e ConfigMap (Kustomize)
```

## Como rodar

```bash
dotnet run --project src/API/Fiap.TechChallenge.OficinaMecanica.Billing.Api.csproj
```

Ou com Docker:

```bash
docker compose up --build
```

A API sobe em `http://localhost:8081`, com Swagger em `/swagger` no ambiente de desenvolvimento.

| Endpoint | Para quê |
|---|---|
| `/health/live` | O processo está de pé (liveness do Kubernetes) |
| `/health/ready` | O serviço e suas dependências estão prontos (readiness) |
| `/health` | Relatório completo em JSON |

## Testes

```bash
dotnet test Fiap.TechChallenge.OficinaMecanica.Billing.slnx --collect:"XPlat Code Coverage" --settings coverlet.runsettings
```

A meta da fase é cobertura mínima de 80%, verificada no SonarCloud ([F4-09](https://github.com/tech-challenge-grupo-160/tech-challenge-oficina-mecanica/issues/140)).

## Fluxo de branches

`feature/*` → `develop` → `homolog` → `main`, com Conventional Commits. Detalhes no [CONTRIBUTING do repositório principal](https://github.com/tech-challenge-grupo-160/tech-challenge-oficina-mecanica/blob/develop/CONTRIBUTING.md).
