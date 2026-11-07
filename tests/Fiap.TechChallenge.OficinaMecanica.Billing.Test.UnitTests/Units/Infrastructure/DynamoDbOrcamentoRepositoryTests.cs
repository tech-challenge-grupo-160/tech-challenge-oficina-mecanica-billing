using System.Globalization;
using System.Text.Json;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Fiap.TechChallenge.OficinaMecanica.Billing.Application.Messaging;
using Fiap.TechChallenge.OficinaMecanica.Billing.Domain.Entities;
using Fiap.TechChallenge.OficinaMecanica.Billing.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;

namespace Fiap.TechChallenge.OficinaMecanica.Billing.Test.UnitTests.Units.Infrastructure;

public class DynamoDbOrcamentoRepositoryTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Criar_DeveGravarOrcamentoInboxEOutboxNaMesmaTransacao()
    {
        TransactWriteItemsRequest? request = null;
        var dynamoDb = new Mock<IAmazonDynamoDB>();
        dynamoDb.Setup(client => client.TransactWriteItemsAsync(
                It.IsAny<TransactWriteItemsRequest>(), It.IsAny<CancellationToken>()))
            .Callback<TransactWriteItemsRequest, CancellationToken>((value, _) => request = value)
            .ReturnsAsync(new TransactWriteItemsResponse());
        var repository = CriarRepository(dynamoDb);
        var orcamento = CriarOrcamento();

        var result = await repository.CriarOuObterAsync(
            orcamento,
            Guid.NewGuid(),
            CriarEvento(orcamento),
            CancellationToken.None);

        result.Criado.Should().BeTrue();
        result.Orcamento.Should().BeSameAs(orcamento);
        request.Should().NotBeNull();
        request!.TransactItems.Should().HaveCount(3);
        request.TransactItems.Select(item => item.Put.Item["EntityType"].S)
            .Should().BeEquivalentTo(["Orcamento", "Inbox", "Outbox"]);
        request.TransactItems.Should().OnlyContain(item =>
            item.Put.ConditionExpression == "attribute_not_exists(PK)");
    }

    [Fact]
    public async Task Obter_DeveUsarLeituraConsistenteERestaurarSnapshots()
    {
        GetItemRequest? request = null;
        var orcamento = CriarOrcamento();
        var dynamoDb = new Mock<IAmazonDynamoDB>();
        dynamoDb.Setup(client => client.GetItemAsync(
                It.IsAny<GetItemRequest>(), It.IsAny<CancellationToken>()))
            .Callback<GetItemRequest, CancellationToken>((value, _) => request = value)
            .ReturnsAsync(new GetItemResponse { Item = Mapear(orcamento) });
        var repository = CriarRepository(dynamoDb);

        var result = await repository.ObterPorOrdemServicoIdAsync(3007, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Id.Should().Be(orcamento.Id);
        result.CorrelationId.Should().Be(orcamento.CorrelationId);
        result.ValorTotal.Should().Be(240m);
        result.Servicos.Should().ContainSingle(item => item.Nome == "Alinhamento");
        result.Pecas.Should().ContainSingle(item => item.Quantidade == 2);
        request!.ConsistentRead.Should().BeTrue();
        request.Key["PK"].S.Should().Be("ORCAMENTO#3007");
    }

    [Fact]
    public async Task Obter_DeveRetornarNuloQuandoNaoExistir()
    {
        var dynamoDb = new Mock<IAmazonDynamoDB>();
        dynamoDb.Setup(client => client.GetItemAsync(
                It.IsAny<GetItemRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetItemResponse());

        var result = await CriarRepository(dynamoDb)
            .ObterPorOrdemServicoIdAsync(3007, CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Criar_DeveSerIdempotenteQuandoTransacaoJaFoiProcessada()
    {
        var orcamento = CriarOrcamento();
        var dynamoDb = new Mock<IAmazonDynamoDB>();
        dynamoDb.Setup(client => client.TransactWriteItemsAsync(
                It.IsAny<TransactWriteItemsRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TransactionCanceledException("conflito"));
        dynamoDb.Setup(client => client.GetItemAsync(
                It.IsAny<GetItemRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetItemResponse { Item = Mapear(orcamento) });

        var result = await CriarRepository(dynamoDb).CriarOuObterAsync(
            orcamento, Guid.NewGuid(), CriarEvento(orcamento), CancellationToken.None);

        result.Criado.Should().BeFalse();
        result.Orcamento.Id.Should().Be(orcamento.Id);
    }

    [Fact]
    public async Task Criar_DeveFalharQuandoOrdemJaTiverOutroDiagnostico()
    {
        var candidato = CriarOrcamento();
        var existente = Orcamento.Gerar(
            Guid.NewGuid(), 3007, 99, "Outro diagnostico",
            [new ItemServicoOrcamento(1000, "Alinhamento", 150m, 30)], [],
            DateTimeOffset.UtcNow);
        var dynamoDb = new Mock<IAmazonDynamoDB>();
        dynamoDb.Setup(client => client.TransactWriteItemsAsync(
                It.IsAny<TransactWriteItemsRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TransactionCanceledException("conflito"));
        dynamoDb.Setup(client => client.GetItemAsync(
                It.IsAny<GetItemRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetItemResponse { Item = Mapear(existente) });

        var action = () => CriarRepository(dynamoDb).CriarOuObterAsync(
            candidato, Guid.NewGuid(), CriarEvento(candidato), CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Decidir_DeveAtualizarOrcamentoEOutboxAtomicamente()
    {
        TransactWriteItemsRequest? request = null;
        var orcamento = CriarOrcamento();
        orcamento.Aprovar(DateTimeOffset.UtcNow);
        var dynamoDb = new Mock<IAmazonDynamoDB>();
        dynamoDb.Setup(client => client.TransactWriteItemsAsync(
                It.IsAny<TransactWriteItemsRequest>(), It.IsAny<CancellationToken>()))
            .Callback<TransactWriteItemsRequest, CancellationToken>((value, _) => request = value)
            .ReturnsAsync(new TransactWriteItemsResponse());

        var result = await CriarRepository(dynamoDb).RegistrarDecisaoAsync(
            orcamento, Guid.NewGuid(), nameof(OrcamentoAprovadoMessage), "{}",
            CancellationToken.None);

        result.Alterado.Should().BeTrue();
        request!.TransactItems.Should().HaveCount(2);
        request.TransactItems[0].Put.Item["Status"].S.Should().Be("Aprovado");
        request.TransactItems[0].Put.Item.Should().ContainKey("RespondidoEm");
        request.TransactItems[1].Put.Item["Tipo"].S.Should().Be(nameof(OrcamentoAprovadoMessage));
    }

    private static DynamoDbOrcamentoRepository CriarRepository(Mock<IAmazonDynamoDB> dynamoDb) =>
        new(dynamoDb.Object, Options.Create(new DynamoDbOptions { TableName = "oficina-billing-test" }));

    private static Orcamento CriarOrcamento() => Orcamento.Gerar(
        Guid.NewGuid(), 3007, 1, "Pastilhas desgastadas.",
        [new ItemServicoOrcamento(1000, "Alinhamento", 150m, 30)],
        [new ItemPecaOrcamento(1000, "Pastilha de Freio", 2, 45m)],
        DateTimeOffset.UtcNow);

    private static OrcamentoGeradoMessage CriarEvento(Orcamento orcamento) => new(
        Guid.NewGuid(), orcamento.CorrelationId, orcamento.CriadoEm, orcamento.Id,
        orcamento.OrdemServicoId, orcamento.DiagnosticoId, orcamento.Status,
        orcamento.ValorServicos, orcamento.ValorPecas, orcamento.ValorTotal);

    private static Dictionary<string, AttributeValue> Mapear(Orcamento orcamento) => new()
    {
        ["PK"] = new($"ORCAMENTO#{orcamento.OrdemServicoId}"),
        ["SK"] = new("ORCAMENTO"),
        ["Id"] = new(orcamento.Id.ToString()),
        ["CorrelationId"] = new(orcamento.CorrelationId.ToString()),
        ["OrdemServicoId"] = new() { N = orcamento.OrdemServicoId.ToString(CultureInfo.InvariantCulture) },
        ["DiagnosticoId"] = new() { N = orcamento.DiagnosticoId.ToString(CultureInfo.InvariantCulture) },
        ["ParecerTecnico"] = new(orcamento.ParecerTecnico),
        ["Status"] = new(orcamento.Status),
        ["CriadoEm"] = new(orcamento.CriadoEm.ToString("O", CultureInfo.InvariantCulture)),
        ["Servicos"] = new(JsonSerializer.Serialize(orcamento.Servicos, JsonOptions)),
        ["Pecas"] = new(JsonSerializer.Serialize(orcamento.Pecas, JsonOptions))
    };
}
