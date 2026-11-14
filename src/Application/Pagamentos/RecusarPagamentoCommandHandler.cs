using Fiap.TechChallenge.OficinaMecanica.Billing.Application.Abstractions;
using Fiap.TechChallenge.OficinaMecanica.Billing.Application.Exceptions;
using MediatR;

namespace Fiap.TechChallenge.OficinaMecanica.Billing.Application.Pagamentos;

/// <summary>
/// Recusa simulada (Development/Testing), para provocar a compensacao de
/// pagamento (F4-27) sem o Mercado Pago. Fora desses ambientes a recusa vem do
/// webhook, em <see cref="ProcessarNotificacaoMercadoPagoCommandHandler"/>.
/// </summary>
public sealed class RecusarPagamentoCommandHandler(IPagamentoRepository pagamentos)
    : IRequestHandler<RecusarPagamentoCommand, PagamentoResult>
{
    public async Task<PagamentoResult> Handle(
        RecusarPagamentoCommand request,
        CancellationToken cancellationToken)
    {
        var pagamento = await pagamentos.ObterAsync(
            request.OrdemServicoId,
            request.PagamentoId,
            cancellationToken)
            ?? throw new ServiceNotFoundException("Pagamento nao encontrado.");

        if (!pagamento.Recusar(DateTimeOffset.UtcNow, request.Motivo))
            return PagamentoResult.Criar(pagamento);

        var resultado = await pagamentos.RecusarAsync(
            pagamento,
            PagamentoEventos.Recusado(pagamento),
            cancellationToken);

        return PagamentoResult.Criar(resultado.Pagamento);
    }
}
