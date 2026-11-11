using Fiap.TechChallenge.OficinaMecanica.Billing.Domain.Entities;

namespace Fiap.TechChallenge.OficinaMecanica.Billing.Application.Abstractions;

/// <summary>
/// Acesso a API do Mercado Pago (Checkout Pro). A implementacao le as
/// credenciais do Secrets Manager; a aplicacao nunca ve o access token.
/// </summary>
public interface IMercadoPagoGateway
{
    /// <summary>
    /// <c>false</c> quando a integracao esta desligada (execucao local e testes
    /// sem credenciais). Nesse caso o pagamento segue pelo fluxo simulado.
    /// </summary>
    bool Habilitado { get; }

    /// <summary>Cria a preferencia de pagamento com referencia a OS.</summary>
    Task<CobrancaMercadoPago> CriarCobrancaAsync(
        Pagamento pagamento,
        CancellationToken cancellationToken);

    /// <summary>
    /// Consulta o pagamento na API. Retorna <c>null</c> quando o Mercado Pago
    /// nao conhece o id informado.
    /// </summary>
    Task<PagamentoMercadoPago?> ObterPagamentoAsync(
        string pagamentoId,
        CancellationToken cancellationToken);
}

public sealed record CobrancaMercadoPago(string Id, string LinkPagamento);

public sealed record PagamentoMercadoPago(
    string Id,
    string Status,
    string? StatusDetalhe,
    string? ReferenciaExterna,
    decimal Valor);
