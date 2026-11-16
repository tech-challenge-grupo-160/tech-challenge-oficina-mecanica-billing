using MediatR;

namespace Fiap.TechChallenge.OficinaMecanica.Billing.Application.Pagamentos;

/// <summary>
/// Notificacao do topico <c>payment</c> com a assinatura ja validada. Carrega so
/// o id: o status e sempre lido na API do Mercado Pago, nunca do corpo.
/// </summary>
public sealed record ProcessarNotificacaoMercadoPagoCommand(string MercadoPagoPagamentoId)
    : IRequest<NotificacaoMercadoPagoResult>;

public sealed record NotificacaoMercadoPagoResult(string Desfecho, Guid? PagamentoId = null)
{
    public const string Confirmado = "Confirmado";
    public const string Recusado = "Recusado";
    public const string Duplicada = "Duplicada";
    public const string Aguardando = "Aguardando";
    public const string Ignorada = "Ignorada";
}
