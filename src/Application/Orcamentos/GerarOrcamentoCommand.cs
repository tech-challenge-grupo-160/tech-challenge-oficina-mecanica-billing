using MediatR;

namespace Fiap.TechChallenge.OficinaMecanica.Billing.Application.Orcamentos;

public sealed record GerarOrcamentoCommand(
    Guid MensagemEntradaId,
    Guid CorrelationId,
    int OrdemServicoId,
    int DiagnosticoId,
    string ParecerTecnico,
    IReadOnlyCollection<ServicoOrcamentoCommand> Servicos,
    IReadOnlyCollection<PecaOrcamentoCommand> Pecas,
    DateTimeOffset ConcluidoEm) : IRequest<OrcamentoResult>;

public sealed record ServicoOrcamentoCommand(
    int ServicoId,
    string Nome,
    decimal Preco,
    int TempoEstimado);

public sealed record PecaOrcamentoCommand(
    int PecaId,
    string Nome,
    int Quantidade,
    decimal Preco);
