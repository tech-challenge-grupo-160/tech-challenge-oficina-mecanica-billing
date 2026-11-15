using Fiap.TechChallenge.OficinaMecanica.Billing.API.Responses.Pagamentos;
using Fiap.TechChallenge.OficinaMecanica.Billing.Application.Pagamentos;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Fiap.TechChallenge.OficinaMecanica.Billing.API.Controllers;

[ApiController]
[Route("api/v1/pagamentos")]
public sealed class PagamentosController(ISender sender, IWebHostEnvironment environment) : ControllerBase
{
    [HttpGet("ordem-servico/{ordemServicoId:int}")]
    [ProducesResponseType<PagamentoResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagamentoResponse>> ObterPorOrdemServico(
        int ordemServicoId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new ObterPagamentoPorOrdemQuery(ordemServicoId),
            cancellationToken);
        return Ok(PagamentoResponse.From(result));
    }

    [HttpPatch("ordem-servico/{ordemServicoId:int}/{pagamentoId:guid}/simular-confirmacao")]
    [ProducesResponseType<PagamentoResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagamentoResponse>> SimularConfirmacao(
        int ordemServicoId,
        Guid pagamentoId,
        CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment() && !environment.IsEnvironment("Testing"))
            return NotFound();

        var result = await sender.Send(
            new ConfirmarPagamentoCommand(ordemServicoId, pagamentoId),
            cancellationToken);
        return Ok(PagamentoResponse.From(result));
    }

    /// <summary>
    /// Provoca a recusa do pagamento (so em Development/Testing) para exercitar
    /// a compensacao de pagamento recusado (F4-27).
    /// </summary>
    [HttpPatch("ordem-servico/{ordemServicoId:int}/{pagamentoId:guid}/simular-recusa")]
    [ProducesResponseType<PagamentoResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagamentoResponse>> SimularRecusa(
        int ordemServicoId,
        Guid pagamentoId,
        [FromQuery] string? motivo,
        CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment() && !environment.IsEnvironment("Testing"))
            return NotFound();

        var result = await sender.Send(
            new RecusarPagamentoCommand(
                ordemServicoId,
                pagamentoId,
                string.IsNullOrWhiteSpace(motivo) ? "simulado: pagamento recusado" : motivo),
            cancellationToken);
        return Ok(PagamentoResponse.From(result));
    }
}
