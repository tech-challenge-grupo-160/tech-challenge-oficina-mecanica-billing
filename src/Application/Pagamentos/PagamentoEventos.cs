using Fiap.TechChallenge.OficinaMecanica.Billing.Application.Messaging;
using Fiap.TechChallenge.OficinaMecanica.Billing.Domain.Entities;

namespace Fiap.TechChallenge.OficinaMecanica.Billing.Application.Pagamentos;

internal static class PagamentoEventos
{
    public static PagamentoConfirmadoMessage Confirmado(Pagamento pagamento) => new(
        Guid.NewGuid(),
        pagamento.CorrelationId,
        pagamento.ConfirmadoEm!.Value,
        pagamento.Id,
        pagamento.OrcamentoId,
        pagamento.OrdemServicoId,
        pagamento.Valor,
        pagamento.Status,
        pagamento.ConfirmadoEm.Value);

    public static PagamentoRecusadoMessage Recusado(Pagamento pagamento) => new(
        Guid.NewGuid(),
        pagamento.CorrelationId,
        pagamento.RecusadoEm!.Value,
        pagamento.Id,
        pagamento.OrcamentoId,
        pagamento.OrdemServicoId,
        pagamento.Valor,
        pagamento.Status,
        pagamento.MotivoRecusa!,
        pagamento.RecusadoEm.Value);
}
