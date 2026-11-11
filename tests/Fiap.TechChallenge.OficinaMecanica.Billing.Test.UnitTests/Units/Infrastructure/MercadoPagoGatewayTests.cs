using System.Net;
using System.Text;
using System.Text.Json;
using Fiap.TechChallenge.OficinaMecanica.Billing.Domain.Entities;
using Fiap.TechChallenge.OficinaMecanica.Billing.Infrastructure.MercadoPago;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Fiap.TechChallenge.OficinaMecanica.Billing.Test.UnitTests.Units.Infrastructure;

public sealed class MercadoPagoGatewayTests
{
    private readonly FakeHandler _handler = new();

    [Fact]
    public async Task CriarCobranca_DeveEnviarPreferenciaComReferenciaDaOs()
    {
        _handler.Responder(HttpStatusCode.Created,
            """{"id":"pref-1","init_point":"https://www.mercadopago.com.br/checkout/v1/redirect?pref_id=pref-1"}""");
        var pagamento = Pagamento.Criar(Guid.NewGuid(), Guid.NewGuid(), 3007, 219.90m, DateTimeOffset.UtcNow);

        var cobranca = await CriarGateway("https://mp.test/notificacoes")
            .CriarCobrancaAsync(pagamento, CancellationToken.None);

        cobranca.Id.Should().Be("pref-1");
        cobranca.LinkPagamento.Should().Contain("pref_id=pref-1");
        var request = _handler.Requests.Single();
        request.Method.Should().Be(HttpMethod.Post);
        request.Uri.Should().Be("https://api.mp.test/checkout/preferences");
        request.Authorization.Should().Be("Bearer TEST-token");
        request.IdempotencyKey.Should().Be(pagamento.Id.ToString());
        using var corpo = JsonDocument.Parse(request.Body!);
        corpo.RootElement.GetProperty("external_reference").GetString()
            .Should().Be($"os:3007;pagamento:{pagamento.Id}");
        corpo.RootElement.GetProperty("notification_url").GetString().Should().Be("https://mp.test/notificacoes");
        var item = corpo.RootElement.GetProperty("items")[0];
        item.GetProperty("unit_price").GetDecimal().Should().Be(219.90m);
        item.GetProperty("currency_id").GetString().Should().Be("BRL");
        item.GetProperty("title").GetString().Should().Be("Ordem de servico 3007");
        corpo.RootElement.GetProperty("metadata").GetProperty("ordem_servico_id").GetString().Should().Be("3007");
    }

    [Fact]
    public async Task CriarCobranca_NaoDeveEnviarNotificationUrlVazia()
    {
        _handler.Responder(HttpStatusCode.Created, """{"id":"pref-1","init_point":"https://mp.test/1"}""");

        await CriarGateway(null).CriarCobrancaAsync(CriarPagamento(), CancellationToken.None);

        using var corpo = JsonDocument.Parse(_handler.Requests.Single().Body!);
        corpo.RootElement.TryGetProperty("notification_url", out _).Should().BeFalse();
    }

    [Fact]
    public async Task CriarCobranca_DeveFalharQuandoMercadoPagoRecusar()
    {
        _handler.Responder(HttpStatusCode.BadRequest, """{"message":"invalid items"}""");

        var action = () => CriarGateway(null).CriarCobrancaAsync(CriarPagamento(), CancellationToken.None);

        (await action.Should().ThrowAsync<HttpRequestException>())
            .Which.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CriarCobranca_DeveFalharSemLink()
    {
        _handler.Responder(HttpStatusCode.Created, """{"id":"pref-1"}""");

        var action = () => CriarGateway(null).CriarCobrancaAsync(CriarPagamento(), CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ObterPagamento_DeveConsultarApiPorId()
    {
        _handler.Responder(HttpStatusCode.OK,
            """{"id":123,"status":"approved","status_detail":"accredited","external_reference":"os:3007;pagamento:x","transaction_amount":219.9}""");

        var pagamento = await CriarGateway(null).ObterPagamentoAsync("123", CancellationToken.None);

        _handler.Requests.Single().Uri.Should().Be("https://api.mp.test/v1/payments/123");
        _handler.Requests.Single().Authorization.Should().Be("Bearer TEST-token");
        pagamento.Should().NotBeNull();
        pagamento!.Id.Should().Be("123");
        pagamento.Status.Should().Be("approved");
        pagamento.StatusDetalhe.Should().Be("accredited");
        pagamento.ReferenciaExterna.Should().Be("os:3007;pagamento:x");
        pagamento.Valor.Should().Be(219.90m);
    }

    [Fact]
    public async Task ObterPagamento_DeveRetornarNuloQuandoNaoExistir()
    {
        _handler.Responder(HttpStatusCode.NotFound, """{"message":"Payment not found"}""");

        var pagamento = await CriarGateway(null).ObterPagamentoAsync("999", CancellationToken.None);

        pagamento.Should().BeNull();
    }

    [Fact]
    public async Task ObterPagamento_DeveFalharEmErroDoServidor()
    {
        _handler.Responder(HttpStatusCode.InternalServerError, "erro");

        var action = () => CriarGateway(null).ObterPagamentoAsync("123", CancellationToken.None);

        await action.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task GatewayDesabilitado_NaoDeveChamarMercadoPago()
    {
        var gateway = new MercadoPagoDesabilitadoGateway();

        gateway.Habilitado.Should().BeFalse();
        await gateway.Invoking(item => item.CriarCobrancaAsync(CriarPagamento(), CancellationToken.None))
            .Should().ThrowAsync<InvalidOperationException>();
        await gateway.Invoking(item => item.ObterPagamentoAsync("123", CancellationToken.None))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    private MercadoPagoGateway CriarGateway(string? notificationUrl)
    {
        var options = Options.Create(new MercadoPagoOptions
        {
            Enabled = true,
            AccessToken = "TEST-token",
            WebhookSecret = "segredo-teste",
            NotificationUrl = notificationUrl
        });
        var client = new HttpClient(_handler) { BaseAddress = new Uri("https://api.mp.test/") };
        var gateway = new MercadoPagoGateway(client, new MercadoPagoCredenciaisProvider(options), options);
        gateway.Habilitado.Should().BeTrue();
        return gateway;
    }

    private static Pagamento CriarPagamento() =>
        Pagamento.Criar(Guid.NewGuid(), Guid.NewGuid(), 3007, 219.90m, DateTimeOffset.UtcNow);

    private sealed record RequestCapturado(
        HttpMethod Method,
        string Uri,
        string? Authorization,
        string? IdempotencyKey,
        string? Body);

    private sealed class FakeHandler : HttpMessageHandler
    {
        private HttpStatusCode _status = HttpStatusCode.OK;
        private string _corpo = "{}";

        public List<RequestCapturado> Requests { get; } = [];

        public void Responder(HttpStatusCode status, string corpo)
        {
            _status = status;
            _corpo = corpo;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(new RequestCapturado(
                request.Method,
                request.RequestUri!.ToString(),
                request.Headers.Authorization?.ToString(),
                request.Headers.TryGetValues("X-Idempotency-Key", out var valores) ? valores.Single() : null,
                request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken)));

            return new HttpResponseMessage(_status)
            {
                Content = new StringContent(_corpo, Encoding.UTF8, "application/json")
            };
        }
    }
}
