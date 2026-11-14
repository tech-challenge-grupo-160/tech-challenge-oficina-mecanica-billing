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

public sealed record OrcamentoAprovadoMessage(
    Guid MessageId,
    Guid CorrelationId,
    DateTimeOffset OcorridoEm,
    Guid OrcamentoId,
    int OrdemServicoId,
    int DiagnosticoId,
    string Status,
    decimal ValorTotal,
    int Versao = 1);

public sealed record OrcamentoRecusadoMessage(
    Guid MessageId,
    Guid CorrelationId,
    DateTimeOffset OcorridoEm,
    Guid OrcamentoId,
    int OrdemServicoId,
    int DiagnosticoId,
    string Status,
    string Motivo,
    int Versao = 1);

public sealed record SolicitarPagamentoMessage(
    Guid MessageId,
    Guid CorrelationId,
    DateTimeOffset OcorridoEm,
    Guid OrcamentoId,
    int OrdemServicoId,
    int Versao = 1,
    int Tentativa = 1);

public sealed record PagamentoConfirmadoMessage(
    Guid MessageId,
    Guid CorrelationId,
    DateTimeOffset OcorridoEm,
    Guid PagamentoId,
    Guid OrcamentoId,
    int OrdemServicoId,
    decimal Valor,
    string Status,
    DateTimeOffset ConfirmadoEm,
    int Versao = 1,
    int Tentativa = 1);

public sealed record PagamentoRecusadoMessage(
    Guid MessageId,
    Guid CorrelationId,
    DateTimeOffset OcorridoEm,
    Guid PagamentoId,
    Guid OrcamentoId,
    int OrdemServicoId,
    decimal Valor,
    string Status,
    string Motivo,
    DateTimeOffset RecusadoEm,
    int Versao = 1,
    int Tentativa = 1);
