using Fiap.TechChallenge.OficinaMecanica.Billing.Application.Orcamentos;

namespace Fiap.TechChallenge.OficinaMecanica.Billing.API.Responses.Orcamentos;

public sealed record OrcamentoResponse(
    Guid Id,
    Guid CorrelationId,
    int OrdemServicoId,
    int DiagnosticoId,
    string ParecerTecnico,
    string Status,
    IReadOnlyCollection<ItemServicoOrcamentoResponse> Servicos,
    IReadOnlyCollection<ItemPecaOrcamentoResponse> Pecas,
    decimal ValorServicos,
    decimal ValorPecas,
    decimal ValorTotal,
    DateTimeOffset CriadoEm,
    DateTimeOffset? RespondidoEm,
    string? MotivoRecusa,
    string UrlAprovacao,
    string UrlRecusa)
{
    public static OrcamentoResponse From(OrcamentoResult result) => new(
        result.Id,
        result.CorrelationId,
        result.OrdemServicoId,
        result.DiagnosticoId,
        result.ParecerTecnico,
        result.Status,
        result.Servicos.Select(item => new ItemServicoOrcamentoResponse(
            item.ServicoId,
            item.Nome,
            item.Preco,
            item.TempoEstimado)).ToArray(),
        result.Pecas.Select(item => new ItemPecaOrcamentoResponse(
            item.PecaId,
            item.Nome,
            item.Quantidade,
            item.Preco)).ToArray(),
        result.ValorServicos,
        result.ValorPecas,
        result.ValorTotal,
        result.CriadoEm,
        result.RespondidoEm,
        result.MotivoRecusa,
        $"/api/v1/orcamentos/{result.OrdemServicoId}/{result.Id}/aprovar",
        $"/api/v1/orcamentos/{result.OrdemServicoId}/{result.Id}/recusar");
}

public sealed record ItemServicoOrcamentoResponse(
    int ServicoId,
    string Nome,
    decimal Preco,
    int TempoEstimado);

public sealed record ItemPecaOrcamentoResponse(
    int PecaId,
    string Nome,
    int Quantidade,
    decimal Preco);
