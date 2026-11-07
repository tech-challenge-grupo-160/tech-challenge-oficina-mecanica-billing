using Fiap.TechChallenge.OficinaMecanica.Billing.Application.Messaging;
using Fiap.TechChallenge.OficinaMecanica.Billing.Domain.Entities;

namespace Fiap.TechChallenge.OficinaMecanica.Billing.Application.Abstractions;

public interface IOrcamentoRepository
{
    Task<(Orcamento Orcamento, bool Criado)> CriarOuObterAsync(
        Orcamento candidato,
        Guid mensagemEntradaId,
        OrcamentoGeradoMessage eventoSaida,
        CancellationToken cancellationToken);

    Task<Orcamento?> ObterPorOrdemServicoIdAsync(
        int ordemServicoId,
        CancellationToken cancellationToken);

    Task<(Orcamento Orcamento, bool Alterado)> RegistrarDecisaoAsync(
        Orcamento orcamento,
        Guid eventoId,
        string tipoEvento,
        string payloadEvento,
        CancellationToken cancellationToken);
}
