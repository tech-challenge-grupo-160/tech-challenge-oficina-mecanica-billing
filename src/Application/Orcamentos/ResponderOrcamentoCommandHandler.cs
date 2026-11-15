using System.Text.Json;
using Fiap.TechChallenge.OficinaMecanica.Billing.Application.Abstractions;
using Fiap.TechChallenge.OficinaMecanica.Billing.Application.Exceptions;
using Fiap.TechChallenge.OficinaMecanica.Billing.Application.Messaging;
using MediatR;

namespace Fiap.TechChallenge.OficinaMecanica.Billing.Application.Orcamentos;

public sealed class ResponderOrcamentoCommandHandler(IOrcamentoRepository repository)
    : IRequestHandler<ResponderOrcamentoCommand, OrcamentoResult>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<OrcamentoResult> Handle(
        ResponderOrcamentoCommand request,
        CancellationToken cancellationToken)
    {
        var orcamento = await repository.ObterPorOrdemServicoIdAsync(
            request.OrdemServicoId,
            cancellationToken)
            ?? throw new ServiceNotFoundException(
                $"Orcamento da ordem de servico {request.OrdemServicoId} nao encontrado.");

        if (orcamento.Id != request.OrcamentoId)
            throw new ServiceNotFoundException("Orcamento nao encontrado.");

        var agora = DateTimeOffset.UtcNow;
        var alterado = request.Aprovado
            ? orcamento.Aprovar(agora)
            : orcamento.Recusar(request.Motivo!, agora);
        if (!alterado)
            return OrcamentoResult.Criar(orcamento);

        var eventoId = Guid.NewGuid();
        object evento = request.Aprovado
            ? new OrcamentoAprovadoMessage(
                eventoId,
                orcamento.CorrelationId,
                agora,
                orcamento.Id,
                orcamento.OrdemServicoId,
                orcamento.DiagnosticoId,
                orcamento.Status,
                orcamento.ValorTotal)
            : new OrcamentoRecusadoMessage(
                eventoId,
                orcamento.CorrelationId,
                agora,
                orcamento.Id,
                orcamento.OrdemServicoId,
                orcamento.DiagnosticoId,
                orcamento.Status,
                orcamento.MotivoRecusa!);

        var resultado = await repository.RegistrarDecisaoAsync(
            orcamento,
            eventoId,
            evento.GetType().Name,
            JsonSerializer.Serialize(evento, evento.GetType(), JsonOptions),
            cancellationToken);

        return OrcamentoResult.Criar(resultado.Orcamento);
    }
}
