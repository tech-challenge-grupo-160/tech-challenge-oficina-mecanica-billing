using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using System.Globalization;
using Fiap.TechChallenge.OficinaMecanica.Billing.Application.Messaging;
using Fiap.TechChallenge.OficinaMecanica.Billing.Domain.Entities;
using Fiap.TechChallenge.OficinaMecanica.Billing.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;

namespace Fiap.TechChallenge.OficinaMecanica.Billing.Test.UnitTests.Units.Infrastructure;

public sealed class DynamoDbPagamentoRepositoryTests
{
    [Fact]
    public async Task Criar_DeveGravarPagamentoEInboxAtomicamente()
    {
        TransactWriteItemsRequest? request = null;
        var dynamo = new Mock<IAmazonDynamoDB>();
        dynamo.Setup(client => client.TransactWriteItemsAsync(
                It.IsAny<TransactWriteItemsRequest>(), It.IsAny<CancellationToken>()))
            .Callback<TransactWriteItemsRequest, CancellationToken>((value, _) => request = value)
            .ReturnsAsync(new TransactWriteItemsResponse());
        var pagamento = CriarPagamento();

        var result = await CriarRepository(dynamo).CriarOuObterAsync(
            pagamento, Guid.NewGuid(), CancellationToken.None);

        result.Criado.Should().BeTrue();
        request!.TransactItems.Should().HaveCount(2);
        request.TransactItems.Select(item => item.Put.Item["EntityType"].S)
            .Should().BeEquivalentTo(["Pagamento", "Inbox"]);
    }

    [Fact]
    public async Task Obter_DeveRestaurarPagamentoPorChaveComposta()
    {
        var pagamento = CriarPagamento();
        GetItemRequest? request = null;
        var dynamo = new Mock<IAmazonDynamoDB>();
        dynamo.Setup(client => client.GetItemAsync(
                It.IsAny<GetItemRequest>(), It.IsAny<CancellationToken>()))
            .Callback<GetItemRequest, CancellationToken>((value, _) => request = value)
            .ReturnsAsync(new GetItemResponse
            {
                Item = Mapear(pagamento)
            });

        var result = await CriarRepository(dynamo).ObterAsync(3007, pagamento.Id, CancellationToken.None);

        result!.Id.Should().Be(pagamento.Id);
        result.Status.Should().Be("Pendente");
        request!.ConsistentRead.Should().BeTrue();
        request.Key["PK"].S.Should().Be("PAGAMENTO#OS#3007");
    }

    [Fact]
    public async Task Confirmar_DeveAtualizarPagamentoECriarOutbox()
    {
        TransactWriteItemsRequest? request = null;
        var dynamo = new Mock<IAmazonDynamoDB>();
        dynamo.Setup(client => client.TransactWriteItemsAsync(
                It.IsAny<TransactWriteItemsRequest>(), It.IsAny<CancellationToken>()))
            .Callback<TransactWriteItemsRequest, CancellationToken>((value, _) => request = value)
            .ReturnsAsync(new TransactWriteItemsResponse());
        var pagamento = CriarPagamento();
        pagamento.Confirmar(DateTimeOffset.UtcNow);
        var evento = new PagamentoConfirmadoMessage(
            Guid.NewGuid(), pagamento.CorrelationId, pagamento.ConfirmadoEm!.Value,
            pagamento.Id, pagamento.OrcamentoId, pagamento.OrdemServicoId,
            pagamento.Valor, pagamento.Status, pagamento.ConfirmadoEm.Value);

        var result = await CriarRepository(dynamo).ConfirmarAsync(
            pagamento, evento, CancellationToken.None);

        result.Alterado.Should().BeTrue();
        request!.TransactItems.Should().HaveCount(2);
        request.TransactItems[0].Put.Item["Status"].S.Should().Be("Confirmado");
        request.TransactItems[1].Put.Item["Tipo"].S.Should().Be(nameof(PagamentoConfirmadoMessage));
    }

    private static DynamoDbPagamentoRepository CriarRepository(Mock<IAmazonDynamoDB> dynamo) =>
        new(dynamo.Object, Options.Create(new DynamoDbOptions { TableName = "oficina-billing-test" }));

    private static Pagamento CriarPagamento() => Pagamento.Criar(
        Guid.NewGuid(), Guid.NewGuid(), 3007, 219.90m, DateTimeOffset.UtcNow);

    private static Dictionary<string, AttributeValue> Mapear(Pagamento pagamento) => new()
    {
        ["PK"] = new($"PAGAMENTO#OS#{pagamento.OrdemServicoId}"),
        ["SK"] = new($"PAGAMENTO#{pagamento.Id}"),
        ["Id"] = new(pagamento.Id.ToString()),
        ["CorrelationId"] = new(pagamento.CorrelationId.ToString()),
        ["OrcamentoId"] = new(pagamento.OrcamentoId.ToString()),
        ["OrdemServicoId"] = new() { N = pagamento.OrdemServicoId.ToString(CultureInfo.InvariantCulture) },
        ["Valor"] = new() { N = pagamento.Valor.ToString(CultureInfo.InvariantCulture) },
        ["Status"] = new(pagamento.Status),
        ["CriadoEm"] = new(pagamento.CriadoEm.ToString("O", CultureInfo.InvariantCulture))
    };
}
