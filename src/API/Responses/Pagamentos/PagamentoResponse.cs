using Fiap.TechChallenge.OficinaMecanica.Billing.Application.Pagamentos;

namespace Fiap.TechChallenge.OficinaMecanica.Billing.API.Responses.Pagamentos;

public sealed record PagamentoResponse(
    Guid Id,
    Guid CorrelationId,
    Guid OrcamentoId,
    int OrdemServicoId,
    decimal Valor,
    string Status,
    DateTimeOffset CriadoEm,
    DateTimeOffset? ConfirmadoEm,
    DateTimeOffset? RecusadoEm,
    string? MotivoRecusa,
    string? LinkPagamento,
    int Tentativa)
{
    public static PagamentoResponse From(PagamentoResult result) => new(
        result.Id,
        result.CorrelationId,
        result.OrcamentoId,
        result.OrdemServicoId,
        result.Valor,
        result.Status,
        result.CriadoEm,
        result.ConfirmadoEm,
        result.RecusadoEm,
        result.MotivoRecusa,
        result.LinkPagamento,
        result.Tentativa);
}
