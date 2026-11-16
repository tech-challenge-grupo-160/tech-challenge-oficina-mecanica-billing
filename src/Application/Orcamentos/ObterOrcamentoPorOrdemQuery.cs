using MediatR;

namespace Fiap.TechChallenge.OficinaMecanica.Billing.Application.Orcamentos;

public sealed record ObterOrcamentoPorOrdemQuery(int OrdemServicoId) : IRequest<OrcamentoResult>;
