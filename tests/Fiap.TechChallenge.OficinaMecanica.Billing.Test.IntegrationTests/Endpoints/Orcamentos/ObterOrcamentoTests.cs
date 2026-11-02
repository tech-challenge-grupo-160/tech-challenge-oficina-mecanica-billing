using System.Net;
using System.Net.Http.Json;
using Fiap.TechChallenge.OficinaMecanica.Billing.API.Responses.Orcamentos;
using Fiap.TechChallenge.OficinaMecanica.Billing.Domain.Entities;
using Fiap.TechChallenge.OficinaMecanica.Billing.Test.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace Fiap.TechChallenge.OficinaMecanica.Billing.Test.IntegrationTests.Endpoints.Orcamentos;

public class ObterOrcamentoTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ObterOrcamentoTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Get_DeveRetornarOrcamentoComSnapshotsETotais()
    {
        _factory.Repository.Orcamento = CriarOrcamento();

        var response = await _client.GetAsync("/api/v1/orcamentos/ordem-servico/3007");
        var body = await response.Content.ReadFromJsonAsync<OrcamentoResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().NotBeNull();
        body!.OrdemServicoId.Should().Be(3007);
        body.Status.Should().Be("Gerado");
        body.Servicos.Should().ContainSingle(item => item.ServicoId == 1000);
        body.Pecas.Should().ContainSingle(item => item.Quantidade == 2);
        body.ValorServicos.Should().Be(150m);
        body.ValorPecas.Should().Be(90m);
        body.ValorTotal.Should().Be(240m);
    }

    [Fact]
    public async Task Get_DeveRetornarNotFoundQuandoOrcamentoNaoExistir()
    {
        _factory.Repository.Orcamento = null;

        var response = await _client.GetAsync("/api/v1/orcamentos/ordem-servico/9999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_DeveRetornarBadRequestQuandoIdForInvalido()
    {
        var response = await _client.GetAsync("/api/v1/orcamentos/ordem-servico/0");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private static Orcamento CriarOrcamento() => Orcamento.Gerar(
        Guid.NewGuid(), 3007, 1, "Pastilhas desgastadas.",
        [new ItemServicoOrcamento(1000, "Alinhamento", 150m, 30)],
        [new ItemPecaOrcamento(1000, "Pastilha de Freio", 2, 45m)],
        DateTimeOffset.UtcNow);
}
