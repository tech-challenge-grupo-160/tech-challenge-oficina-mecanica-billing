namespace Fiap.TechChallenge.OficinaMecanica.Billing.Domain.Entities;

public sealed class Pagamento
{
    private Pagamento() { }

    public Guid Id { get; private set; }
    public Guid CorrelationId { get; private set; }
    public Guid OrcamentoId { get; private set; }
    public int OrdemServicoId { get; private set; }
    public decimal Valor { get; private set; }
    public string Status { get; private set; } = null!;
    public DateTimeOffset CriadoEm { get; private set; }
    public DateTimeOffset? ConfirmadoEm { get; private set; }

    public static Pagamento Criar(
        Guid correlationId,
        Guid orcamentoId,
        int ordemServicoId,
        decimal valor,
        DateTimeOffset criadoEm)
    {
        if (correlationId == Guid.Empty)
            throw new ArgumentException("CorrelationId e obrigatorio.", nameof(correlationId));
        if (orcamentoId == Guid.Empty)
            throw new ArgumentException("OrcamentoId e obrigatorio.", nameof(orcamentoId));
        if (ordemServicoId <= 0)
            throw new ArgumentException("OrdemServicoId deve ser maior que zero.", nameof(ordemServicoId));
        if (valor <= 0)
            throw new ArgumentException("Valor do pagamento deve ser maior que zero.", nameof(valor));

        return new Pagamento
        {
            Id = Guid.NewGuid(),
            CorrelationId = correlationId,
            OrcamentoId = orcamentoId,
            OrdemServicoId = ordemServicoId,
            Valor = valor,
            Status = "Pendente",
            CriadoEm = criadoEm
        };
    }

    public bool Confirmar(DateTimeOffset confirmadoEm)
    {
        if (Status == "Confirmado")
            return false;
        if (Status != "Pendente")
            throw new InvalidOperationException($"Pagamento nao pode ser confirmado no status {Status}.");

        Status = "Confirmado";
        ConfirmadoEm = confirmadoEm;
        return true;
    }

    public bool Corresponde(Guid correlationId, Guid orcamentoId) =>
        CorrelationId == correlationId && OrcamentoId == orcamentoId;

    public static Pagamento Restaurar(
        Guid id,
        Guid correlationId,
        Guid orcamentoId,
        int ordemServicoId,
        decimal valor,
        string status,
        DateTimeOffset criadoEm,
        DateTimeOffset? confirmadoEm) => new()
        {
            Id = id,
            CorrelationId = correlationId,
            OrcamentoId = orcamentoId,
            OrdemServicoId = ordemServicoId,
            Valor = valor,
            Status = status,
            CriadoEm = criadoEm,
            ConfirmadoEm = confirmadoEm
        };
}
