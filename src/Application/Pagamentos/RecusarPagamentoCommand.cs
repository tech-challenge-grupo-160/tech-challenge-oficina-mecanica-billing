using MediatR;

namespace Fiap.TechChallenge.OficinaMecanica.Billing.Application.Pagamentos;

public sealed record RecusarPagamentoCommand(
    int OrdemServicoId,
    Guid PagamentoId,
    string Motivo) : IRequest<PagamentoResult>;
