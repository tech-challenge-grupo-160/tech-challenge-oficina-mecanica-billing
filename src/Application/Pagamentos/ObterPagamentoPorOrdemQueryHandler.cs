using Fiap.TechChallenge.OficinaMecanica.Billing.Application.Abstractions;
using Fiap.TechChallenge.OficinaMecanica.Billing.Application.Exceptions;
using MediatR;

namespace Fiap.TechChallenge.OficinaMecanica.Billing.Application.Pagamentos;

public sealed class ObterPagamentoPorOrdemQueryHandler(IPagamentoRepository pagamentos)
    : IRequestHandler<ObterPagamentoPorOrdemQuery, PagamentoResult>
{
    public async Task<PagamentoResult> Handle(
        ObterPagamentoPorOrdemQuery request,
        CancellationToken cancellationToken)
    {
        var pagamento = await pagamentos.ObterPorOrdemServicoIdAsync(
            request.OrdemServicoId,
            cancellationToken)
            ?? throw new ServiceNotFoundException("Pagamento nao encontrado.");
        return PagamentoResult.Criar(pagamento);
    }
}
