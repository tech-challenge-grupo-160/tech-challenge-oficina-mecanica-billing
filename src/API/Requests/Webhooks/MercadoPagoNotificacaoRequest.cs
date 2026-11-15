namespace Fiap.TechChallenge.OficinaMecanica.Billing.API.Requests.Webhooks;

/// <summary>
/// Corpo da notificacao do Mercado Pago. So o tipo e o id sao lidos: o status
/// do pagamento vem sempre da consulta na API.
/// </summary>
public sealed record MercadoPagoNotificacaoRequest(
    string? Type,
    string? Action,
    MercadoPagoNotificacaoDataRequest? Data);

public sealed record MercadoPagoNotificacaoDataRequest(string? Id);
