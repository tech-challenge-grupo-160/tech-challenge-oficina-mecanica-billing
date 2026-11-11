namespace Fiap.TechChallenge.OficinaMecanica.Billing.Domain.Entities;

public sealed class Pagamento
{
    public const string StatusPendente = "Pendente";
    public const string StatusConfirmado = "Confirmado";
    public const string StatusRecusado = "Recusado";

    private Pagamento() { }

    public Guid Id { get; private set; }
    public Guid CorrelationId { get; private set; }
    public Guid OrcamentoId { get; private set; }
    public int OrdemServicoId { get; private set; }
    public decimal Valor { get; private set; }
    public string Status { get; private set; } = null!;
    public DateTimeOffset CriadoEm { get; private set; }
    public DateTimeOffset? ConfirmadoEm { get; private set; }
    public DateTimeOffset? RecusadoEm { get; private set; }
    public string? MotivoRecusa { get; private set; }

    /// <summary>Id da preferencia (Checkout Pro) criada no Mercado Pago.</summary>
    public string? CobrancaId { get; private set; }

    /// <summary>Link (init_point) para o cliente pagar na pagina do Mercado Pago.</summary>
    public string? LinkPagamento { get; private set; }

    /// <summary>Id do pagamento no Mercado Pago que levou ao desfecho.</summary>
    public string? MercadoPagoPagamentoId { get; private set; }

    public bool PossuiCobranca => !string.IsNullOrWhiteSpace(CobrancaId);

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
            Status = StatusPendente,
            CriadoEm = criadoEm
        };
    }

    /// <summary>
    /// Registra a cobranca criada no Mercado Pago. Idempotente: uma cobranca ja
    /// registrada nao e substituida.
    /// </summary>
    public bool RegistrarCobranca(string cobrancaId, string linkPagamento)
    {
        if (string.IsNullOrWhiteSpace(cobrancaId))
            throw new ArgumentException("Id da cobranca e obrigatorio.", nameof(cobrancaId));
        if (string.IsNullOrWhiteSpace(linkPagamento))
            throw new ArgumentException("Link de pagamento e obrigatorio.", nameof(linkPagamento));
        if (PossuiCobranca)
            return false;
        if (Status != StatusPendente)
            throw new InvalidOperationException($"Cobranca nao pode ser criada no status {Status}.");

        CobrancaId = cobrancaId;
        LinkPagamento = linkPagamento;
        return true;
    }

    public bool Confirmar(DateTimeOffset confirmadoEm, string? mercadoPagoPagamentoId = null)
    {
        if (Status == StatusConfirmado)
            return false;
        if (Status != StatusPendente)
            throw new InvalidOperationException($"Pagamento nao pode ser confirmado no status {Status}.");

        Status = StatusConfirmado;
        ConfirmadoEm = confirmadoEm;
        MercadoPagoPagamentoId = mercadoPagoPagamentoId;
        return true;
    }

    public bool Recusar(DateTimeOffset recusadoEm, string motivo, string? mercadoPagoPagamentoId = null)
    {
        if (string.IsNullOrWhiteSpace(motivo))
            throw new ArgumentException("Motivo da recusa e obrigatorio.", nameof(motivo));
        if (Status == StatusRecusado)
            return false;
        if (Status != StatusPendente)
            throw new InvalidOperationException($"Pagamento nao pode ser recusado no status {Status}.");

        Status = StatusRecusado;
        RecusadoEm = recusadoEm;
        MotivoRecusa = motivo;
        MercadoPagoPagamentoId = mercadoPagoPagamentoId;
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
        DateTimeOffset? confirmadoEm,
        DateTimeOffset? recusadoEm = null,
        string? motivoRecusa = null,
        string? cobrancaId = null,
        string? linkPagamento = null,
        string? mercadoPagoPagamentoId = null) => new()
        {
            Id = id,
            CorrelationId = correlationId,
            OrcamentoId = orcamentoId,
            OrdemServicoId = ordemServicoId,
            Valor = valor,
            Status = status,
            CriadoEm = criadoEm,
            ConfirmadoEm = confirmadoEm,
            RecusadoEm = recusadoEm,
            MotivoRecusa = motivoRecusa,
            CobrancaId = cobrancaId,
            LinkPagamento = linkPagamento,
            MercadoPagoPagamentoId = mercadoPagoPagamentoId
        };
}
