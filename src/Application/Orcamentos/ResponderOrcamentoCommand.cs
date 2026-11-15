using MediatR;

namespace Fiap.TechChallenge.OficinaMecanica.Billing.Application.Orcamentos;

public sealed record ResponderOrcamentoCommand(
    int OrdemServicoId,
    Guid OrcamentoId,
    bool Aprovado,
    string? Motivo)
    : IRequest<OrcamentoResult>;
