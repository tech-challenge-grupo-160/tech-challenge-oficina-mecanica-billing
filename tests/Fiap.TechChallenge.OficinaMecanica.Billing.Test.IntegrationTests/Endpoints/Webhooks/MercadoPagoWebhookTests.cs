using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Fiap.TechChallenge.OficinaMecanica.Billing.API.Controllers;
using Fiap.TechChallenge.OficinaMecanica.Billing.Application.Abstractions;
using Fiap.TechChallenge.OficinaMecanica.Billing.Application.Messaging;
using Fiap.TechChallenge.OficinaMecanica.Billing.Application.Pagamentos;
using Fiap.TechChallenge.OficinaMecanica.Billing.Domain.Entities;
using Fiap.TechChallenge.OficinaMecanica.Billing.Test.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Fiap.TechChallenge.OficinaMecanica.Billing.Test.IntegrationTests.Endpoints.Webhooks;

public sealed class MercadoPagoWebhookTests : IClassFixture<MercadoPagoWebApplicationFactory>
{
    private const string Rota = "/api/v1/pagamentos/webhook";
    private readonly MercadoPagoWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly Pagamento _pagamento;

    public MercadoPagoWebhookTests(MercadoPagoWebApplicationFactory factory)
    {
        _factory = factory;
        _pagamento = Pagamento.Criar(Guid.NewGuid(), Guid.NewGuid(), 3007, 219.90m, DateTimeOffset.UtcNow);
        _factory.Pagamentos.Pagamento = _pagamento;
        _factory.Pagamentos.Eventos.Clear();
        _factory.Gateway.Consultas = 0;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Post_DeveConfirmarPagamentoAprovadoConsultandoAApi()
    {
        _factory.Gateway.Responder("approved", _pagamento);

        // O corpo diz "rejected": deve valer o status consultado na API.
        var response = await Enviar("123", corpo: """{"type":"payment","action":"payment.updated","data":{"id":"123","status":"rejected"}}""");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<NotificacaoMercadoPagoResponse>();
        body!.Desfecho.Should().Be(NotificacaoMercadoPagoResult.Confirmado);
        body.PagamentoId.Should().Be(_pagamento.Id);
        _factory.Gateway.Consultas.Should().Be(1);
        _factory.Pagamentos.Eventos.Should().ContainSingle().Which.Should().BeOfType<PagamentoConfirmadoMessage>();
    }

    [Fact]
    public async Task Post_DevePublicarRecusaDefinitiva()
    {
        _factory.Gateway.Responder("cancelled", _pagamento);

        var response = await Enviar("123");

        var body = await response.Content.ReadFromJsonAsync<NotificacaoMercadoPagoResponse>();
        body!.Desfecho.Should().Be(NotificacaoMercadoPagoResult.Recusado);
        _factory.Pagamentos.Eventos.Should().ContainSingle().Which.Should().BeOfType<PagamentoRecusadoMessage>();
    }

    [Fact]
    public async Task Post_TentativaRecusadaNaoDevePublicarRecusa()
    {
        // Checkout Pro: depois de um rejected o comprador pode tentar de novo.
        _factory.Gateway.Responder("rejected", _pagamento);

        var response = await Enviar("123");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<NotificacaoMercadoPagoResponse>();
        body!.Desfecho.Should().Be(NotificacaoMercadoPagoResult.Aguardando);
        _factory.Pagamentos.Eventos.Should().BeEmpty();
    }

    [Fact]
    public async Task Post_RepetidoDevePublicarUmaUnicaVez()
    {
        _factory.Gateway.Responder("approved", _pagamento);

        var primeira = await Enviar("123");
        var segunda = await Enviar("123");

        primeira.StatusCode.Should().Be(HttpStatusCode.OK);
        segunda.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await segunda.Content.ReadFromJsonAsync<NotificacaoMercadoPagoResponse>();
        body!.Desfecho.Should().Be(NotificacaoMercadoPagoResult.Duplicada);
        _factory.Pagamentos.Eventos.Should().ContainSingle();
    }

    [Fact]
    public async Task Post_ComAssinaturaInvalidaDeveSerRecusadoSemConsultarApi()
    {
        _factory.Gateway.Responder("approved", _pagamento);

        var response = await Enviar("123", segredo: "segredo-errado");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        _factory.Gateway.Consultas.Should().Be(0);
        _factory.Pagamentos.Eventos.Should().BeEmpty();
    }

    [Fact]
    public async Task Post_SemAssinaturaDeveSerRecusado()
    {
        var response = await _client.PostAsync($"{Rota}?data.id=123&type=payment", null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_DeOutroTopicoDeveSerIgnorado()
    {
        var response = await Enviar("123", tipo: "merchant_order");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<NotificacaoMercadoPagoResponse>();
        body!.Desfecho.Should().Be(NotificacaoMercadoPagoResult.Ignorada);
        _factory.Gateway.Consultas.Should().Be(0);
    }

    [Fact]
    public async Task Post_DeveAceitarIdSoNoCorpo()
    {
        _factory.Gateway.Responder("approved", _pagamento);
        var request = new HttpRequestMessage(HttpMethod.Post, Rota)
        {
            Content = new StringContent("""{"type":"payment","data":{"id":"123"}}""", Encoding.UTF8, "application/json")
        };
        request.Headers.Add("x-request-id", "req-1");
        request.Headers.Add("x-signature", Assinar("id:123;request-id:req-1;ts:1;", "1", MercadoPagoWebApplicationFactory.Segredo));

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _factory.Gateway.Consultas.Should().Be(1);
    }

    private Task<HttpResponseMessage> Enviar(
        string dataId,
        string tipo = "payment",
        string segredo = MercadoPagoWebApplicationFactory.Segredo,
        string? corpo = null)
    {
        const string requestId = "req-1";
        const string ts = "1704908010";
        var request = new HttpRequestMessage(HttpMethod.Post, $"{Rota}?data.id={dataId}&type={tipo}")
        {
            Content = new StringContent(
                corpo ?? $$$"""{"type":"{{{tipo}}}","action":"payment.updated","data":{"id":"{{{dataId}}}"}}""",
                Encoding.UTF8,
                "application/json")
        };
        request.Headers.Add("x-request-id", requestId);
        request.Headers.Add("x-signature", Assinar($"id:{dataId};request-id:{requestId};ts:{ts};", ts, segredo));
        return _client.SendAsync(request);
    }

    private static string Assinar(string manifesto, string ts, string segredo) =>
        $"ts={ts},v1=" + Convert.ToHexStringLower(HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(segredo),
            Encoding.UTF8.GetBytes(manifesto)));
}

public sealed class MercadoPagoWebApplicationFactory : CustomWebApplicationFactory
{
    public const string Segredo = "segredo-webhook-teste";

    public FakeMercadoPagoGateway Gateway { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("MercadoPago:Enabled", "true");
        builder.UseSetting("MercadoPago:AccessToken", "TEST-token");
        builder.UseSetting("MercadoPago:WebhookSecret", Segredo);
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IMercadoPagoGateway>();
            services.AddSingleton<IMercadoPagoGateway>(Gateway);
        });
    }
}

public sealed class FakeMercadoPagoGateway : IMercadoPagoGateway
{
    private PagamentoMercadoPago? _resposta;

    public int Consultas { get; set; }
    public bool Habilitado => true;

    public void Responder(string status, Pagamento pagamento) =>
        _resposta = new PagamentoMercadoPago(
            "123",
            status,
            null,
            ReferenciaExterna.Criar(pagamento.OrdemServicoId, pagamento.Id),
            pagamento.Valor);

    public Task<CobrancaMercadoPago> CriarCobrancaAsync(Pagamento pagamento, CancellationToken cancellationToken) =>
        Task.FromResult(new CobrancaMercadoPago("pref-1", "https://mp.test/1"));

    public Task<PagamentoMercadoPago?> ObterPagamentoAsync(string pagamentoId, CancellationToken cancellationToken)
    {
        Consultas++;
        return Task.FromResult(_resposta);
    }
}
