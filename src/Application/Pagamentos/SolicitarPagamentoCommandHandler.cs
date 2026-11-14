using Fiap.TechChallenge.OficinaMecanica.Billing.Application.Abstractions;
using Fiap.TechChallenge.OficinaMecanica.Billing.Application.Exceptions;
using Fiap.TechChallenge.OficinaMecanica.Billing.Domain.Entities;
using MediatR;

namespace Fiap.TechChallenge.OficinaMecanica.Billing.Application.Pagamentos;

public sealed class SolicitarPagamentoCommandHandler(
    IOrcamentoRepository orcamentos,
    IPagamentoRepository pagamentos,
    IMercadoPagoGateway mercadoPago)
    : IRequestHandler<SolicitarPagamentoCommand, PagamentoResult>
{
    public async Task<PagamentoResult> Handle(
        SolicitarPagamentoCommand request,
        CancellationToken cancellationToken)
    {
        var orcamento = await orcamentos.ObterPorOrdemServicoIdAsync(
            request.OrdemServicoId,
            cancellationToken)
            ?? throw new ServiceNotFoundException("Orcamento aprovado nao encontrado para cobranca.");

        if (orcamento.Id != request.OrcamentoId ||
            orcamento.CorrelationId != request.CorrelationId)
            throw new InvalidOperationException("Solicitacao de pagamento nao corresponde ao orcamento.");
        if (orcamento.Status != "Aprovado")
            throw new InvalidOperationException("Somente orcamento aprovado pode gerar pagamento.");

        // Nova tentativa (F4-27): so depois que a anterior foi recusada. O
        // pagamento e o passo repetivel da saga (ADR-0004): nunca existem duas
        // cobrancas abertas para a mesma OS.
        if (request.Tentativa > 1)
        {
            var anterior = await pagamentos.ObterAsync(
                request.OrdemServicoId,
                Pagamento.GerarId(request.CorrelationId, request.Tentativa - 1),
                cancellationToken);
            if (anterior?.Status != Pagamento.StatusRecusado)
                throw new InvalidOperationException(
                    $"Nova tentativa de cobranca ({request.Tentativa}) exige a tentativa anterior recusada.");
        }

        var candidato = Pagamento.Criar(
            request.CorrelationId,
            request.OrcamentoId,
            request.OrdemServicoId,
            orcamento.ValorTotal,
            request.SolicitadoEm,
            request.Tentativa);
        var (pagamento, _) = await pagamentos.CriarOuObterAsync(
            candidato,
            request.MensagemEntradaId,
            cancellationToken);

        // Tambem cobre o reprocessamento: se a mensagem voltou porque a chamada
        // ao Mercado Pago falhou, o pagamento ja existe e so falta a cobranca.
        if (mercadoPago.Habilitado &&
            pagamento.Status == Pagamento.StatusPendente &&
            !pagamento.PossuiCobranca)
        {
            var cobranca = await mercadoPago.CriarCobrancaAsync(pagamento, cancellationToken);
            pagamento.RegistrarCobranca(cobranca.Id, cobranca.LinkPagamento);
            pagamento = await pagamentos.RegistrarCobrancaAsync(pagamento, cancellationToken);
        }

        return PagamentoResult.Criar(pagamento);
    }
}
