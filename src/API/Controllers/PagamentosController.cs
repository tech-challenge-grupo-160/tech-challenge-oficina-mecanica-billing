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
}
