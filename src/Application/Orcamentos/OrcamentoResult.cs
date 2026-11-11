using Fiap.TechChallenge.OficinaMecanica.Billing.Domain.Entities;

namespace Fiap.TechChallenge.OficinaMecanica.Billing.Application.Orcamentos;

public sealed record OrcamentoResult(
    Guid Id,
    Guid CorrelationId,
    int OrdemServicoId,
    int DiagnosticoId,
    string ParecerTecnico,
    string Status,
    IReadOnlyCollection<ItemServicoOrcamento> Servicos,
    IReadOnlyCollection<ItemPecaOrcamento> Pecas,
    decimal ValorServicos,
    decimal ValorPecas,
    decimal ValorTotal,
    DateTimeOffset CriadoEm,
    DateTimeOffset? RespondidoEm,
    string? MotivoRecusa,
    bool Criado)
{
    public static OrcamentoResult Criar(Orcamento orcamento, bool criado = false) => new(
        orcamento.Id,
        orcamento.CorrelationId,
        orcamento.OrdemServicoId,
        orcamento.DiagnosticoId,
        orcamento.ParecerTecnico,
        orcamento.Status,
        orcamento.Servicos,
        orcamento.Pecas,
        orcamento.ValorServicos,
        orcamento.ValorPecas,
        orcamento.ValorTotal,
        orcamento.CriadoEm,
        orcamento.RespondidoEm,
        orcamento.MotivoRecusa,
        criado);
}
