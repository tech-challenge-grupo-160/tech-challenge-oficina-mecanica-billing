using Fiap.TechChallenge.OficinaMecanica.Billing.Domain.Entities;

namespace Fiap.TechChallenge.OficinaMecanica.Billing.Application.Pagamentos;

public sealed record PagamentoResult(
    Guid Id,
    Guid CorrelationId,
    Guid OrcamentoId,
    int OrdemServicoId,
    decimal Valor,
    string Status,
    DateTimeOffset CriadoEm,
    DateTimeOffset? ConfirmadoEm,
    DateTimeOffset? RecusadoEm = null,
    string? MotivoRecusa = null,
    string? LinkPagamento = null,
    int Tentativa = 1)
{
    public static PagamentoResult Criar(Pagamento pagamento) => new(
        pagamento.Id,
        pagamento.CorrelationId,
        pagamento.OrcamentoId,
        pagamento.OrdemServicoId,
        pagamento.Valor,
        pagamento.Status,
        pagamento.CriadoEm,
        pagamento.ConfirmadoEm,
        pagamento.RecusadoEm,
        pagamento.MotivoRecusa,
        pagamento.LinkPagamento,
        pagamento.Tentativa);
}
