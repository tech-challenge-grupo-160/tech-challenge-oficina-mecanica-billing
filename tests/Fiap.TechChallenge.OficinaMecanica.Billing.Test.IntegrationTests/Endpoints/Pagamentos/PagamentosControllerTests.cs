using System.Net;
using System.Net.Http.Json;
using Fiap.TechChallenge.OficinaMecanica.Billing.API.Responses.Pagamentos;
using Fiap.TechChallenge.OficinaMecanica.Billing.Domain.Entities;
using Fiap.TechChallenge.OficinaMecanica.Billing.Test.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace Fiap.TechChallenge.OficinaMecanica.Billing.Test.IntegrationTests.Endpoints.Pagamentos;

public sealed class PagamentosControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public PagamentosControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.Pagamentos.Pagamento = Pagamento.Criar(
            Guid.NewGuid(), Guid.NewGuid(), 3007, 219.90m, DateTimeOffset.UtcNow);
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Get_DeveRetornarPagamentoPendente()
    {
        var response = await _client.GetAsync("/api/v1/pagamentos/ordem-servico/3007");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagamentoResponse>();
        body!.Status.Should().Be("Pendente");
        body.Valor.Should().Be(219.90m);
    }

    [Fact]
    public async Task PatchSimulado_DeveConfirmarPagamentoIdempotentemente()
    {
        var pagamento = _factory.Pagamentos.Pagamento!;
        var endpoint = $"/api/v1/pagamentos/ordem-servico/3007/{pagamento.Id}/simular-confirmacao";

        var primeira = await _client.PatchAsync(endpoint, null);
        var segunda = await _client.PatchAsync(endpoint, null);

        primeira.StatusCode.Should().Be(HttpStatusCode.OK);
        segunda.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await segunda.Content.ReadFromJsonAsync<PagamentoResponse>();
        body!.Status.Should().Be("Confirmado");
        body.ConfirmadoEm.Should().NotBeNull();
    }

    [Fact]
    public async Task Get_DeveRetornarNotFoundQuandoPagamentoNaoExistir()
    {
        _factory.Pagamentos.Pagamento = null;

        var response = await _client.GetAsync("/api/v1/pagamentos/ordem-servico/9999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
