using Fiap.TechChallenge.OficinaMecanica.Billing.Application.Abstractions;
using Fiap.TechChallenge.OficinaMecanica.Billing.Application.Exceptions;
using Fiap.TechChallenge.OficinaMecanica.Billing.Domain.Entities;
using MediatR;

namespace Fiap.TechChallenge.OficinaMecanica.Billing.Application.Pagamentos;

public sealed class SolicitarPagamentoCommandHandler(
    IOrcamentoRepository orcamentos,
    IPagamentoRepository pagamentos)
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

        var candidato = Pagamento.Criar(
            request.CorrelationId,
            request.OrcamentoId,
            request.OrdemServicoId,
            orcamento.ValorTotal,
            request.SolicitadoEm);
        var resultado = await pagamentos.CriarOuObterAsync(
            candidato,
            request.MensagemEntradaId,
            cancellationToken);

        return PagamentoResult.Criar(resultado.Pagamento);
    }
}
