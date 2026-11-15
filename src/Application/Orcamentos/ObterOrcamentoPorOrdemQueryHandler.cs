using Fiap.TechChallenge.OficinaMecanica.Billing.Application.Abstractions;
using Fiap.TechChallenge.OficinaMecanica.Billing.Application.Exceptions;
using MediatR;

namespace Fiap.TechChallenge.OficinaMecanica.Billing.Application.Orcamentos;

public sealed class ObterOrcamentoPorOrdemQueryHandler(IOrcamentoRepository repository)
    : IRequestHandler<ObterOrcamentoPorOrdemQuery, OrcamentoResult>
{
    public async Task<OrcamentoResult> Handle(
        ObterOrcamentoPorOrdemQuery request,
        CancellationToken cancellationToken)
    {
        var orcamento = await repository.ObterPorOrdemServicoIdAsync(
            request.OrdemServicoId,
            cancellationToken)
            ?? throw new ServiceNotFoundException(
                $"Orcamento da ordem de servico {request.OrdemServicoId} nao encontrado.");

        return OrcamentoResult.Criar(orcamento);
    }
}
