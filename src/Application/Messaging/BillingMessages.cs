namespace Fiap.TechChallenge.OficinaMecanica.Billing.Application.Messaging;

public sealed record GerarOrcamentoMessage(
    Guid MessageId,
    Guid CorrelationId,
    DateTimeOffset OcorridoEm,
    int OrdemServicoId,
    int DiagnosticoId,
    string ParecerTecnico,
    IReadOnlyCollection<ServicoGerarOrcamentoMessage> Servicos,
    IReadOnlyCollection<PecaGerarOrcamentoMessage> Pecas,
    DateTimeOffset DiagnosticoConcluidoEm,
    int Versao = 1);

public sealed record ServicoGerarOrcamentoMessage(
    int ServicoId,
    string Nome,
    decimal Preco,
    int TempoEstimado);

public sealed record PecaGerarOrcamentoMessage(
    int PecaId,
    string Nome,
    int Quantidade,
    decimal Preco);

public sealed record OrcamentoGeradoMessage(
    Guid MessageId,
    Guid CorrelationId,
    DateTimeOffset OcorridoEm,
    Guid OrcamentoId,
    int OrdemServicoId,
    int DiagnosticoId,
    string Status,
    decimal ValorServicos,
    decimal ValorPecas,
    decimal ValorTotal,
    int Versao = 1);
