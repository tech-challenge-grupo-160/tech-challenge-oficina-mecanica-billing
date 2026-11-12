using Fiap.TechChallenge.OficinaMecanica.Billing.Application.Messaging;
using Fiap.TechChallenge.OficinaMecanica.Billing.Domain.Entities;

namespace Fiap.TechChallenge.OficinaMecanica.Billing.Application.Abstractions;

public interface IPagamentoRepository
{
    Task<(Pagamento Pagamento, bool Criado)> CriarOuObterAsync(
        Pagamento candidato,
        Guid mensagemEntradaId,
        CancellationToken cancellationToken);

    Task<Pagamento?> ObterPorOrdemServicoIdAsync(
        int ordemServicoId,
        CancellationToken cancellationToken);

    Task<Pagamento?> ObterAsync(
        int ordemServicoId,
        Guid pagamentoId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Grava a cobranca do Mercado Pago no pagamento. Se outra execucao ja
    /// gravou uma cobranca, devolve o pagamento persistido sem alterar.
    /// </summary>
    Task<Pagamento> RegistrarCobrancaAsync(
        Pagamento pagamento,
        CancellationToken cancellationToken);

    Task<(Pagamento Pagamento, bool Alterado)> ConfirmarAsync(
        Pagamento pagamento,
        PagamentoConfirmadoMessage evento,
        CancellationToken cancellationToken);

    Task<(Pagamento Pagamento, bool Alterado)> RecusarAsync(
        Pagamento pagamento,
        PagamentoRecusadoMessage evento,
        CancellationToken cancellationToken);
}
