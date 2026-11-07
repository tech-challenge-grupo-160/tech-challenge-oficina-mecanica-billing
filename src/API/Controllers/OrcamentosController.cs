using Fiap.TechChallenge.OficinaMecanica.Billing.API.Responses.Orcamentos;
using Fiap.TechChallenge.OficinaMecanica.Billing.API.Requests.Orcamentos;
using Fiap.TechChallenge.OficinaMecanica.Billing.Application.Orcamentos;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Fiap.TechChallenge.OficinaMecanica.Billing.API.Controllers;

[ApiController]
[Route("api/v1/orcamentos")]
public sealed class OrcamentosController(ISender sender) : ControllerBase
{
    [HttpGet("ordem-servico/{ordemServicoId:int}")]
    [ProducesResponseType<OrcamentoResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrcamentoResponse>> ObterPorOrdemServico(
        int ordemServicoId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new ObterOrcamentoPorOrdemQuery(ordemServicoId),
            cancellationToken);

        return Ok(OrcamentoResponse.From(result));
    }

    [HttpPatch("{ordemServicoId:int}/{orcamentoId:guid}/aprovar")]
    [ProducesResponseType<OrcamentoResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<OrcamentoResponse>> Aprovar(
        int ordemServicoId,
        Guid orcamentoId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new ResponderOrcamentoCommand(ordemServicoId, orcamentoId, true, null),
            cancellationToken);

        return Ok(OrcamentoResponse.From(result));
    }

    [HttpPatch("{ordemServicoId:int}/{orcamentoId:guid}/recusar")]
    [ProducesResponseType<OrcamentoResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<OrcamentoResponse>> Recusar(
        int ordemServicoId,
        Guid orcamentoId,
        RecusarOrcamentoRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new ResponderOrcamentoCommand(
                ordemServicoId,
                orcamentoId,
                false,
                request.Motivo),
            cancellationToken);

        return Ok(OrcamentoResponse.From(result));
    }
}
