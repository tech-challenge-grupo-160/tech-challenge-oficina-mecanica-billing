# Changelog

Todas as mudancas notaveis do servico serao documentadas neste arquivo.

O formato segue [Keep a Changelog](https://keepachangelog.com/pt-BR/1.1.0/).

## [Unreleased]

### Added
- Esqueleto do servico: Clean Architecture, MediatR com validacao, health checks, logs em JSON, Dockerfile com o Datadog Tracer, manifests Kubernetes e CI de build e testes (F4-07)
- Terraform por serviço para o tópico SNS do Billing, filas de eventos/comandos, DLQs, filtros, criptografia, retenção e configuração no Secrets Manager (F4-11)
- Workflow independente para validar, planejar e aplicar a infraestrutura de mensageria por ambiente
