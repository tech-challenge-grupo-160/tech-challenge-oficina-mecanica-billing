# Changelog

Todas as mudancas notaveis do servico serao documentadas neste arquivo.

O formato segue [Keep a Changelog](https://keepachangelog.com/pt-BR/1.1.0/).

## [Unreleased]

### Changed
- CI/CD pelo pipeline reutilizavel `servico-dotnet.yml@v1` do `tech-challenge-pipelines`: build, testes com cobertura e imagem em todo PR; deploy no EKS no push da `homolog` e da `main`, dev pelo disparo manual (F4-08)

### Added
- Esqueleto do servico: Clean Architecture, MediatR com validacao, health checks, logs em JSON, Dockerfile com o Datadog Tracer, manifests Kubernetes e CI de build e testes (F4-07)
