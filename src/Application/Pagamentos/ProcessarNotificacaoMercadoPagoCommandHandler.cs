using Fiap.TechChallenge.OficinaMecanica.Billing.Application.Abstractions;
using Fiap.TechChallenge.OficinaMecanica.Billing.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Fiap.TechChallenge.OficinaMecanica.Billing.Application.Pagamentos;

/// <summary>
/// Trata a notificacao de pagamento do Mercado Pago.
///
/// O corpo da notificacao nao e confiavel: o handler consulta o pagamento na
/// API e decide pelo status devolvido por ela. A idempotencia vem do estado do
/// pagamento (so sai de Pendente uma vez) e da escrita condicional no
/// repositorio, entao notificacoes repetidas nao publicam o evento de novo.
/// </summary>
public sealed class ProcessarNotificacaoMercadoPagoCommandHandler(
    IMercadoPagoGateway mercadoPago,
    IPagamentoRepository pagamentos,
    ILogger<ProcessarNotificacaoMercadoPagoCommandHandler> logger)
    : IRequestHandler<ProcessarNotificacaoMercadoPagoCommand, NotificacaoMercadoPagoResult>
{
    private static readonly HashSet<string> StatusRecusa =
        new(StringComparer.OrdinalIgnoreCase) { "rejected", "cancelled" };

    public async Task<NotificacaoMercadoPagoResult> Handle(
        ProcessarNotificacaoMercadoPagoCommand request,
        CancellationToken cancellationToken)
    {
        var pagamentoMp = await mercadoPago.ObterPagamentoAsync(
            request.MercadoPagoPagamentoId,
            cancellationToken);
        if (pagamentoMp is null)
            return Ignorar(request.MercadoPagoPagamentoId, "pagamento nao encontrado no Mercado Pago");

        if (!ReferenciaExterna.TentarLer(
                pagamentoMp.ReferenciaExterna,
                out var ordemServicoId,
                out var pagamentoId))
            return Ignorar(pagamentoMp.Id, "referencia externa ausente ou de outro sistema");

        var pagamento = await pagamentos.ObterAsync(ordemServicoId, pagamentoId, cancellationToken);
        if (pagamento is null)
            return Ignorar(pagamentoMp.Id, "pagamento da referencia nao existe no Billing");

        if (pagamentoMp.Valor != pagamento.Valor)
            return Ignorar(pagamentoMp.Id, "valor pago diferente do valor cobrado", pagamento.Id);

        if (string.Equals(pagamentoMp.Status, "approved", StringComparison.OrdinalIgnoreCase))
            return await ConfirmarAsync(pagamento, pagamentoMp, cancellationToken);

        if (StatusRecusa.Contains(pagamentoMp.Status))
            return await RecusarAsync(pagamento, pagamentoMp, cancellationToken);

        // pending, in_process, authorized...: o Mercado Pago avisa de novo
        // quando o status mudar.
        return new NotificacaoMercadoPagoResult(NotificacaoMercadoPagoResult.Aguardando, pagamento.Id);
    }

    private async Task<NotificacaoMercadoPagoResult> ConfirmarAsync(
        Pagamento pagamento,
        PagamentoMercadoPago pagamentoMp,
        CancellationToken cancellationToken)
    {
        if (pagamento.Status == Pagamento.StatusRecusado)
            return Ignorar(pagamentoMp.Id, "aprovacao chegou depois da recusa", pagamento.Id);
        if (!pagamento.Confirmar(DateTimeOffset.UtcNow, pagamentoMp.Id))
            return new NotificacaoMercadoPagoResult(NotificacaoMercadoPagoResult.Duplicada, pagamento.Id);

        var (_, alterado) = await pagamentos.ConfirmarAsync(
            pagamento,
            PagamentoEventos.Confirmado(pagamento),
            cancellationToken);
        return new NotificacaoMercadoPagoResult(
            alterado ? NotificacaoMercadoPagoResult.Confirmado : NotificacaoMercadoPagoResult.Duplicada,
            pagamento.Id);
    }

    private async Task<NotificacaoMercadoPagoResult> RecusarAsync(
        Pagamento pagamento,
        PagamentoMercadoPago pagamentoMp,
        CancellationToken cancellationToken)
    {
        if (pagamento.Status == Pagamento.StatusConfirmado)
            return Ignorar(pagamentoMp.Id, "recusa chegou depois da confirmacao", pagamento.Id);

        var motivo = string.IsNullOrWhiteSpace(pagamentoMp.StatusDetalhe)
            ? pagamentoMp.Status
            : $"{pagamentoMp.Status}: {pagamentoMp.StatusDetalhe}";
        if (!pagamento.Recusar(DateTimeOffset.UtcNow, motivo, pagamentoMp.Id))
            return new NotificacaoMercadoPagoResult(NotificacaoMercadoPagoResult.Duplicada, pagamento.Id);

        var (_, alterado) = await pagamentos.RecusarAsync(
            pagamento,
            PagamentoEventos.Recusado(pagamento),
            cancellationToken);
        return new NotificacaoMercadoPagoResult(
            alterado ? NotificacaoMercadoPagoResult.Recusado : NotificacaoMercadoPagoResult.Duplicada,
            pagamento.Id);
    }

    private NotificacaoMercadoPagoResult Ignorar(
        string mercadoPagoPagamentoId,
        string motivo,
        Guid? pagamentoId = null)
    {
        logger.LogWarning(
            "Notificacao do Mercado Pago ignorada. Pagamento MP {MercadoPagoPagamentoId}: {Motivo}.",
            mercadoPagoPagamentoId,
            motivo);
        return new NotificacaoMercadoPagoResult(NotificacaoMercadoPagoResult.Ignorada, pagamentoId);
    }
}
