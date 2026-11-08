using MediatR;

namespace Fiap.TechChallenge.OficinaMecanica.Billing.Application.Pagamentos;

public sealed record ObterPagamentoPorOrdemQuery(int OrdemServicoId) : IRequest<PagamentoResult>;
