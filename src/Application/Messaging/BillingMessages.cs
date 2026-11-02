namespace Fiap.TechChallenge.OficinaMecanica.Billing.Application.Messaging;

public sealed record DiagnosticoConcluidoMessage(
    Guid MessageId,
    Guid CorrelationId,
    DateTimeOffset OcorridoEm,
    int OrdemServicoId,
    int DiagnosticoId,
    string Status,
    string ParecerTecnico,
    IReadOnlyCollection<ServicoDiagnosticoConcluidoMessage> Servicos,
    IReadOnlyCollection<PecaDiagnosticoConcluidoMessage> Pecas,
    DateTimeOffset ConcluidoEm,
    int Versao = 1);

public sealed record ServicoDiagnosticoConcluidoMessage(
    int ServicoId,
    string Nome,
    decimal Preco,
    int TempoEstimado);

public sealed record PecaDiagnosticoConcluidoMessage(
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
