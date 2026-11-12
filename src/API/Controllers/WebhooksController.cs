using Fiap.TechChallenge.OficinaMecanica.Billing.API.Requests.Webhooks;
using Fiap.TechChallenge.OficinaMecanica.Billing.Application.Abstractions;
using Fiap.TechChallenge.OficinaMecanica.Billing.Application.Pagamentos;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Fiap.TechChallenge.OficinaMecanica.Billing.API.Controllers;

/// <summary>
/// Notificacoes do Mercado Pago. A rota e publica no API Gateway (o Mercado
/// Pago nao manda token do sistema); a protecao e a validacao do
/// <c>x-signature</c> com a assinatura secreta do webhook.
/// </summary>
[ApiController]
[Route("api/v1/pagamentos")]
public sealed class WebhooksController(
    ISender sender,
    IMercadoPagoAssinaturaValidator assinatura,
    IMercadoPagoGateway mercadoPago,
    ILogger<WebhooksController> logger) : ControllerBase
{
    private const string TopicoPagamento = "payment";

    [HttpPost("webhook")]
    [ProducesResponseType<NotificacaoMercadoPagoResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<NotificacaoMercadoPagoResponse>> MercadoPago(
        [FromHeader(Name = "x-signature")] string? xSignature,
        [FromHeader(Name = "x-request-id")] string? xRequestId,
        [FromQuery(Name = "data.id")] string? dataIdQuery,
        [FromQuery(Name = "type")] string? tipoQuery,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] MercadoPagoNotificacaoRequest? corpo,
        CancellationToken cancellationToken)
    {
        // Desligado na configuracao ou sem credencial valida: sem assinatura para
        // validar a notificacao, a rota se comporta como inexistente.
        if (!mercadoPago.Habilitado)
            return NotFound();

        // O manifesto assinado usa o data.id da query string; o corpo so cobre
        // o caso de a query nao trazer o id.
        var dataId = string.IsNullOrWhiteSpace(dataIdQuery) ? corpo?.Data?.Id : dataIdQuery;
        if (!assinatura.Validar(xSignature, xRequestId, dataId))
        {
            logger.LogWarning(
                "Notificacao do Mercado Pago recusada: assinatura invalida. Request {XRequestId}.",
                xRequestId);
            return Unauthorized();
        }

        var tipo = string.IsNullOrWhiteSpace(tipoQuery) ? corpo?.Type : tipoQuery;
        if (!string.Equals(tipo, TopicoPagamento, StringComparison.OrdinalIgnoreCase))
            return Ok(new NotificacaoMercadoPagoResponse(NotificacaoMercadoPagoResult.Ignorada, null));

        if (string.IsNullOrWhiteSpace(dataId))
            return BadRequest();

        var resultado = await sender.Send(
            new ProcessarNotificacaoMercadoPagoCommand(dataId),
            cancellationToken);
        return Ok(new NotificacaoMercadoPagoResponse(resultado.Desfecho, resultado.PagamentoId));
    }
}

public sealed record NotificacaoMercadoPagoResponse(string Desfecho, Guid? PagamentoId);
