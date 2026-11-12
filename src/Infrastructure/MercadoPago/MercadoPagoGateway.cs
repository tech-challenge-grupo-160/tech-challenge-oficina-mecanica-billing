using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Fiap.TechChallenge.OficinaMecanica.Billing.Application.Abstractions;
using Fiap.TechChallenge.OficinaMecanica.Billing.Application.Pagamentos;
using Fiap.TechChallenge.OficinaMecanica.Billing.Domain.Entities;
using Microsoft.Extensions.Options;

namespace Fiap.TechChallenge.OficinaMecanica.Billing.Infrastructure.MercadoPago;

/// <summary>
/// Cliente do Checkout Pro: cria a preferencia de pagamento e consulta o
/// pagamento pelo id recebido no webhook.
/// </summary>
public sealed class MercadoPagoGateway(
    HttpClient httpClient,
    MercadoPagoCredenciaisProvider credenciais,
    IOptions<MercadoPagoOptions> options) : IMercadoPagoGateway
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    // Ligado na configuracao, mas sem credencial valida, se comporta como o
    // gateway desligado: o pagamento nao gera cobranca e o webhook responde 404.
    public bool Habilitado => credenciais.Disponivel;

    public async Task<CobrancaMercadoPago> CriarCobrancaAsync(
        Pagamento pagamento,
        CancellationToken cancellationToken)
    {
        var referencia = ReferenciaExterna.Criar(pagamento.OrdemServicoId, pagamento.Id);
        var preferencia = new PreferenciaRequest(
            [
                new ItemPreferencia(
                    pagamento.OrcamentoId.ToString(),
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"Ordem de servico {pagamento.OrdemServicoId}"),
                    1,
                    "BRL",
                    pagamento.Valor)
            ],
            referencia,
            string.IsNullOrWhiteSpace(options.Value.NotificationUrl) ? null : options.Value.NotificationUrl,
            new Dictionary<string, string>
            {
                ["ordem_servico_id"] = pagamento.OrdemServicoId.ToString(CultureInfo.InvariantCulture),
                ["pagamento_id"] = pagamento.Id.ToString(),
                ["orcamento_id"] = pagamento.OrcamentoId.ToString(),
                ["correlation_id"] = pagamento.CorrelationId.ToString()
            });

        using var request = CriarRequest(HttpMethod.Post, "checkout/preferences");
        // Reenvios do mesmo pagamento (mensagem reprocessada) reutilizam a chave.
        request.Headers.Add("X-Idempotency-Key", pagamento.Id.ToString());
        request.Content = JsonContent.Create(preferencia, options: JsonOptions);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        await GarantirSucessoAsync(response, "criar a preferencia de pagamento", cancellationToken);

        var corpo = await response.Content.ReadFromJsonAsync<PreferenciaResponse>(JsonOptions, cancellationToken);
        if (corpo is null || string.IsNullOrWhiteSpace(corpo.Id) || string.IsNullOrWhiteSpace(corpo.InitPoint))
            throw new InvalidOperationException("Mercado Pago devolveu preferencia sem id ou link de pagamento.");

        return new CobrancaMercadoPago(corpo.Id, corpo.InitPoint);
    }

    public async Task<PagamentoMercadoPago?> ObterPagamentoAsync(
        string pagamentoId,
        CancellationToken cancellationToken)
    {
        using var request = CriarRequest(HttpMethod.Get, $"v1/payments/{Uri.EscapeDataString(pagamentoId)}");
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;
        await GarantirSucessoAsync(response, "consultar o pagamento", cancellationToken);

        var corpo = await response.Content.ReadFromJsonAsync<PagamentoResponse>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("Mercado Pago devolveu pagamento vazio.");

        return new PagamentoMercadoPago(
            corpo.Id.ToString(CultureInfo.InvariantCulture),
            corpo.Status ?? string.Empty,
            corpo.StatusDetail,
            corpo.ExternalReference,
            corpo.TransactionAmount);
    }

    private HttpRequestMessage CriarRequest(HttpMethod method, string caminho)
    {
        var request = new HttpRequestMessage(method, caminho);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credenciais.Obter().AccessToken);
        return request;
    }

    private static async Task GarantirSucessoAsync(
        HttpResponseMessage response,
        string operacao,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return;

        // O corpo de erro do Mercado Pago nao traz credencial; e truncado para
        // nao inflar o log.
        var corpo = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new HttpRequestException(
            $"Falha ao {operacao} no Mercado Pago: HTTP {(int)response.StatusCode}. " +
            corpo[..Math.Min(corpo.Length, 500)],
            null,
            response.StatusCode);
    }

    private sealed record PreferenciaRequest(
        IReadOnlyCollection<ItemPreferencia> Items,
        string ExternalReference,
        string? NotificationUrl,
        IReadOnlyDictionary<string, string> Metadata);

    private sealed record ItemPreferencia(
        string Id,
        string Title,
        int Quantity,
        string CurrencyId,
        decimal UnitPrice);

    private sealed record PreferenciaResponse(string? Id, string? InitPoint);

    private sealed record PagamentoResponse(
        long Id,
        string? Status,
        string? StatusDetail,
        string? ExternalReference,
        decimal TransactionAmount);
}

/// <summary>Usado quando <c>MercadoPago:Enabled</c> e false.</summary>
public sealed class MercadoPagoDesabilitadoGateway : IMercadoPagoGateway
{
    public bool Habilitado => false;

    public Task<CobrancaMercadoPago> CriarCobrancaAsync(
        Pagamento pagamento,
        CancellationToken cancellationToken) =>
        throw new InvalidOperationException("Integracao com o Mercado Pago desabilitada.");

    public Task<PagamentoMercadoPago?> ObterPagamentoAsync(
        string pagamentoId,
        CancellationToken cancellationToken) =>
        throw new InvalidOperationException("Integracao com o Mercado Pago desabilitada.");
}
