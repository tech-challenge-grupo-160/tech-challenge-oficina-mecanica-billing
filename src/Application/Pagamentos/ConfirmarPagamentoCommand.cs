using MediatR;

namespace Fiap.TechChallenge.OficinaMecanica.Billing.Application.Pagamentos;

public sealed record ConfirmarPagamentoCommand(
    int OrdemServicoId,
    Guid PagamentoId) : IRequest<PagamentoResult>;
