using Fiap.TechChallenge.OficinaMecanica.Billing.Application.Abstractions;
using Fiap.TechChallenge.OficinaMecanica.Billing.Application.Exceptions;
using MediatR;

namespace Fiap.TechChallenge.OficinaMecanica.Billing.Application.Pagamentos;

/// <summary>
/// Confirmacao simulada (Development/Testing). Fora desses ambientes a
/// confirmacao vem do webhook do Mercado Pago, em
/// <see cref="ProcessarNotificacaoMercadoPagoCommandHandler"/>.
/// </summary>
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

        var resultado = await pagamentos.ConfirmarAsync(
            pagamento,
            PagamentoEventos.Confirmado(pagamento),
            cancellationToken);

        return PagamentoResult.Criar(resultado.Pagamento);
    }
}
