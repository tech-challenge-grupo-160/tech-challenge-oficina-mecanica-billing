using Fiap.TechChallenge.OficinaMecanica.Billing.Application.Abstractions;
using Fiap.TechChallenge.OficinaMecanica.Billing.Application.Exceptions;
using Fiap.TechChallenge.OficinaMecanica.Billing.Application.Messaging;
using MediatR;

namespace Fiap.TechChallenge.OficinaMecanica.Billing.Application.Pagamentos;

public sealed class ConfirmarPagamentoCommandHandler(IPagamentoRepository pagamentos)
    : IRequestHandler<ConfirmarPagamentoCommand, PagamentoResult>
{
    public async Task<PagamentoResult> Handle(
        ConfirmarPagamentoCommand request,
        CancellationToken cancellationToken)
    {
        var pagamento = await pagamentos.ObterAsync(
            request.OrdemServicoId,
            request.PagamentoId,
            cancellationToken)
            ?? throw new ServiceNotFoundException("Pagamento nao encontrado.");

        if (!pagamento.Confirmar(DateTimeOffset.UtcNow))
            return PagamentoResult.Criar(pagamento);

        var evento = new PagamentoConfirmadoMessage(
            Guid.NewGuid(),
            pagamento.CorrelationId,
            pagamento.ConfirmadoEm!.Value,
            pagamento.Id,
            pagamento.OrcamentoId,
            pagamento.OrdemServicoId,
            pagamento.Valor,
            pagamento.Status,
            pagamento.ConfirmadoEm.Value);
        var resultado = await pagamentos.ConfirmarAsync(
            pagamento,
            evento,
            cancellationToken);

        return PagamentoResult.Criar(resultado.Pagamento);
    }
}
