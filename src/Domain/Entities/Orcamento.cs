namespace Fiap.TechChallenge.OficinaMecanica.Billing.Domain.Entities;

public sealed class Orcamento
{
    private Orcamento() { }

    public Guid Id { get; private set; }
    public Guid CorrelationId { get; private set; }
    public int OrdemServicoId { get; private set; }
    public int DiagnosticoId { get; private set; }
    public string ParecerTecnico { get; private set; } = null!;
    public string Status { get; private set; } = null!;
    public DateTimeOffset CriadoEm { get; private set; }
    public DateTimeOffset? RespondidoEm { get; private set; }
    public string? MotivoRecusa { get; private set; }
    public IReadOnlyCollection<ItemServicoOrcamento> Servicos { get; private set; } = [];
    public IReadOnlyCollection<ItemPecaOrcamento> Pecas { get; private set; } = [];
    public decimal ValorServicos => Servicos.Sum(item => item.Preco);
    public decimal ValorPecas => Pecas.Sum(item => item.Preco * item.Quantidade);
    public decimal ValorTotal => ValorServicos + ValorPecas;

    public static Orcamento Gerar(
        Guid correlationId,
        int ordemServicoId,
        int diagnosticoId,
        string parecerTecnico,
        IEnumerable<ItemServicoOrcamento> servicos,
        IEnumerable<ItemPecaOrcamento> pecas,
        DateTimeOffset criadoEm)
    {
        if (correlationId == Guid.Empty)
            throw new ArgumentException("CorrelationId e obrigatorio.", nameof(correlationId));
        if (ordemServicoId <= 0)
            throw new ArgumentException("OrdemServicoId deve ser maior que zero.", nameof(ordemServicoId));
        if (diagnosticoId <= 0)
            throw new ArgumentException("DiagnosticoId deve ser maior que zero.", nameof(diagnosticoId));
        if (string.IsNullOrWhiteSpace(parecerTecnico))
            throw new ArgumentException("Parecer tecnico e obrigatorio.", nameof(parecerTecnico));

        var itensServico = servicos.OrderBy(item => item.ServicoId).ToArray();
        if (itensServico.Length == 0)
            throw new ArgumentException("Ao menos um servico e obrigatorio.", nameof(servicos));
        if (itensServico.Select(item => item.ServicoId).Distinct().Count() != itensServico.Length)
            throw new ArgumentException("Nao e permitido repetir servicos.", nameof(servicos));

        var itensPeca = pecas.OrderBy(item => item.PecaId).ToArray();
        if (itensPeca.Select(item => item.PecaId).Distinct().Count() != itensPeca.Length)
            throw new ArgumentException("Nao e permitido repetir pecas.", nameof(pecas));

        return new Orcamento
        {
            Id = Guid.NewGuid(),
            CorrelationId = correlationId,
            OrdemServicoId = ordemServicoId,
            DiagnosticoId = diagnosticoId,
            ParecerTecnico = parecerTecnico.Trim(),
            Status = "Gerado",
            CriadoEm = criadoEm,
            Servicos = itensServico,
            Pecas = itensPeca
        };
    }

    public bool CorrespondeAoDiagnostico(Guid correlationId, int diagnosticoId) =>
        CorrelationId == correlationId && DiagnosticoId == diagnosticoId;

    public bool Aprovar(DateTimeOffset respondidoEm)
    {
        if (Status == "Aprovado")
            return false;
        if (Status != "Gerado")
            throw new InvalidOperationException($"Orcamento nao pode ser aprovado no status {Status}.");

        Status = "Aprovado";
        RespondidoEm = respondidoEm;
        MotivoRecusa = null;
        return true;
    }

    public bool Recusar(string motivo, DateTimeOffset respondidoEm)
    {
        if (Status == "Recusado")
            return false;
        if (Status != "Gerado")
            throw new InvalidOperationException($"Orcamento nao pode ser recusado no status {Status}.");
        if (string.IsNullOrWhiteSpace(motivo))
            throw new ArgumentException("Motivo da recusa e obrigatorio.", nameof(motivo));

        Status = "Recusado";
        RespondidoEm = respondidoEm;
        MotivoRecusa = motivo.Trim();
        return true;
    }

    public static Orcamento Restaurar(
        Guid id,
        Guid correlationId,
        int ordemServicoId,
        int diagnosticoId,
        string parecerTecnico,
        string status,
        DateTimeOffset criadoEm,
        IReadOnlyCollection<ItemServicoOrcamento> servicos,
        IReadOnlyCollection<ItemPecaOrcamento> pecas,
        DateTimeOffset? respondidoEm = null,
        string? motivoRecusa = null) => new()
        {
            Id = id,
            CorrelationId = correlationId,
            OrdemServicoId = ordemServicoId,
            DiagnosticoId = diagnosticoId,
            ParecerTecnico = parecerTecnico,
            Status = status,
            CriadoEm = criadoEm,
            RespondidoEm = respondidoEm,
            MotivoRecusa = motivoRecusa,
            Servicos = servicos,
            Pecas = pecas
        };
}

public sealed record ItemServicoOrcamento(
    int ServicoId,
    string Nome,
    decimal Preco,
    int TempoEstimado)
{
    public ItemServicoOrcamento Validar()
    {
        if (ServicoId <= 0 || string.IsNullOrWhiteSpace(Nome) || Preco < 0 || TempoEstimado <= 0)
            throw new ArgumentException("Item de servico invalido.");
        return this;
    }
}

public sealed record ItemPecaOrcamento(
    int PecaId,
    string Nome,
    int Quantidade,
    decimal Preco)
{
    public ItemPecaOrcamento Validar()
    {
        if (PecaId <= 0 || string.IsNullOrWhiteSpace(Nome) || Quantidade <= 0 || Preco < 0)
            throw new ArgumentException("Item de peca invalido.");
        return this;
    }
}
