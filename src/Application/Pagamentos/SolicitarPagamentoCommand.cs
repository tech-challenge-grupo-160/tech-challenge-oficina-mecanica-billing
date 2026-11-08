using MediatR;

namespace Fiap.TechChallenge.OficinaMecanica.Billing.Application.Pagamentos;

public sealed record SolicitarPagamentoCommand(
    Guid MensagemEntradaId,
    Guid CorrelationId,
    Guid OrcamentoId,
    int OrdemServicoId,
    DateTimeOffset SolicitadoEm) : IRequest<PagamentoResult>;
