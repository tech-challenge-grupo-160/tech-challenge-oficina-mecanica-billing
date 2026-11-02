using Fiap.TechChallenge.OficinaMecanica.Billing.Application.Abstractions;
using Fiap.TechChallenge.OficinaMecanica.Billing.Application.Messaging;
using Fiap.TechChallenge.OficinaMecanica.Billing.Domain.Entities;
using MediatR;

namespace Fiap.TechChallenge.OficinaMecanica.Billing.Application.Orcamentos;

public sealed class GerarOrcamentoCommandHandler(IOrcamentoRepository repository)
    : IRequestHandler<GerarOrcamentoCommand, OrcamentoResult>
{
    public async Task<OrcamentoResult> Handle(
        GerarOrcamentoCommand request,
        CancellationToken cancellationToken)
    {
        var orcamento = Orcamento.Gerar(
            request.CorrelationId,
            request.OrdemServicoId,
            request.DiagnosticoId,
            request.ParecerTecnico,
            request.Servicos.Select(item => new ItemServicoOrcamento(
                item.ServicoId,
                item.Nome,
                item.Preco,
                item.TempoEstimado).Validar()),
            request.Pecas.Select(item => new ItemPecaOrcamento(
                item.PecaId,
                item.Nome,
                item.Quantidade,
                item.Preco).Validar()),
            DateTimeOffset.UtcNow);

        var evento = new OrcamentoGeradoMessage(
            Guid.NewGuid(),
            orcamento.CorrelationId,
            orcamento.CriadoEm,
            orcamento.Id,
            orcamento.OrdemServicoId,
            orcamento.DiagnosticoId,
            orcamento.Status,
            orcamento.ValorServicos,
            orcamento.ValorPecas,
            orcamento.ValorTotal);

        var (persistido, criado) = await repository.CriarOuObterAsync(
            orcamento,
            request.MensagemEntradaId,
            evento,
            cancellationToken);

        if (!persistido.CorrespondeAoDiagnostico(request.CorrelationId, request.DiagnosticoId))
            throw new InvalidOperationException(
                "A ordem de servico ja possui um orcamento para outro diagnostico.");

        return OrcamentoResult.Criar(persistido, criado);
    }
}
