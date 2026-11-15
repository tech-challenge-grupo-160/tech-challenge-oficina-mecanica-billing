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

    [Fact]
    public async Task Recusar_DeveAtualizarPagamentoECriarOutboxDeRecusa()
    {
        TransactWriteItemsRequest? request = null;
        var dynamo = new Mock<IAmazonDynamoDB>();
        dynamo.Setup(client => client.TransactWriteItemsAsync(
                It.IsAny<TransactWriteItemsRequest>(), It.IsAny<CancellationToken>()))
            .Callback<TransactWriteItemsRequest, CancellationToken>((value, _) => request = value)
            .ReturnsAsync(new TransactWriteItemsResponse());
        var pagamento = CriarPagamento();
        pagamento.RegistrarCobranca("pref-1", "https://mp.test/1");
        pagamento.Recusar(DateTimeOffset.UtcNow, "rejected: cc_rejected_other_reason", "123");
        var evento = new PagamentoRecusadoMessage(
            Guid.NewGuid(), pagamento.CorrelationId, pagamento.RecusadoEm!.Value,
            pagamento.Id, pagamento.OrcamentoId, pagamento.OrdemServicoId,
            pagamento.Valor, pagamento.Status, pagamento.MotivoRecusa!, pagamento.RecusadoEm.Value);

        var result = await CriarRepository(dynamo).RecusarAsync(pagamento, evento, CancellationToken.None);

        result.Alterado.Should().BeTrue();
        var item = request!.TransactItems[0].Put;
        item.ConditionExpression.Should().Contain("#status = :pendente");
        item.Item["Status"].S.Should().Be("Recusado");
        item.Item["MotivoRecusa"].S.Should().Be("rejected: cc_rejected_other_reason");
        item.Item["MercadoPagoPagamentoId"].S.Should().Be("123");
        item.Item["CobrancaId"].S.Should().Be("pref-1");
        request.TransactItems[1].Put.Item["Tipo"].S.Should().Be(nameof(PagamentoRecusadoMessage));
    }

    [Fact]
    public async Task Confirmar_DeveTratarConcorrenciaComoDuplicada()
    {
        var pagamento = CriarPagamento();
        pagamento.Confirmar(DateTimeOffset.UtcNow, "123");
        var dynamo = new Mock<IAmazonDynamoDB>();
        dynamo.Setup(client => client.TransactWriteItemsAsync(
                It.IsAny<TransactWriteItemsRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TransactionCanceledException("condicao"));
        dynamo.Setup(client => client.GetItemAsync(It.IsAny<GetItemRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetItemResponse { Item = Mapear(pagamento) });
        var evento = new PagamentoConfirmadoMessage(
            Guid.NewGuid(), pagamento.CorrelationId, pagamento.ConfirmadoEm!.Value,
            pagamento.Id, pagamento.OrcamentoId, pagamento.OrdemServicoId,
            pagamento.Valor, pagamento.Status, pagamento.ConfirmadoEm.Value);

        var result = await CriarRepository(dynamo).ConfirmarAsync(pagamento, evento, CancellationToken.None);

        result.Alterado.Should().BeFalse();
        result.Pagamento.MercadoPagoPagamentoId.Should().Be("123");
    }

    [Fact]
    public async Task Recusar_DeveFalharQuandoOutroDesfechoFoiGravado()
    {
        var gravado = CriarPagamento();
        gravado.Confirmar(DateTimeOffset.UtcNow);
        var pagamento = Pagamento.Restaurar(
            gravado.Id, gravado.CorrelationId, gravado.OrcamentoId, gravado.OrdemServicoId,
            gravado.Valor, Pagamento.StatusPendente, gravado.CriadoEm, null);
        pagamento.Recusar(DateTimeOffset.UtcNow, "rejected");
        var dynamo = new Mock<IAmazonDynamoDB>();
        dynamo.Setup(client => client.TransactWriteItemsAsync(
                It.IsAny<TransactWriteItemsRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TransactionCanceledException("condicao"));
        dynamo.Setup(client => client.GetItemAsync(It.IsAny<GetItemRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetItemResponse { Item = Mapear(gravado) });
        var evento = new PagamentoRecusadoMessage(
            Guid.NewGuid(), pagamento.CorrelationId, pagamento.RecusadoEm!.Value,
            pagamento.Id, pagamento.OrcamentoId, pagamento.OrdemServicoId,
            pagamento.Valor, pagamento.Status, pagamento.MotivoRecusa!, pagamento.RecusadoEm.Value);

        var action = () => CriarRepository(dynamo).RecusarAsync(pagamento, evento, CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task RegistrarCobranca_DeveGravarCobrancaCondicionalmente()
    {
        UpdateItemRequest? request = null;
        var dynamo = new Mock<IAmazonDynamoDB>();
        dynamo.Setup(client => client.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), It.IsAny<CancellationToken>()))
            .Callback<UpdateItemRequest, CancellationToken>((value, _) => request = value)
            .ReturnsAsync(new UpdateItemResponse());
        var pagamento = CriarPagamento();
        pagamento.RegistrarCobranca("pref-1", "https://mp.test/1");

        var result = await CriarRepository(dynamo).RegistrarCobrancaAsync(pagamento, CancellationToken.None);

        result.Should().BeSameAs(pagamento);
        request!.ConditionExpression.Should().Contain("attribute_not_exists(CobrancaId)");
        request.ExpressionAttributeValues[":cobrancaId"].S.Should().Be("pref-1");
        request.ExpressionAttributeValues[":link"].S.Should().Be("https://mp.test/1");
        request.Key["PK"].S.Should().Be("PAGAMENTO#OS#3007");
    }

    [Fact]
    public async Task RegistrarCobranca_DeveDevolverCobrancaJaGravada()
    {
        var gravado = CriarPagamento();
        gravado.RegistrarCobranca("pref-1", "https://mp.test/1");
        var pagamento = Pagamento.Restaurar(
            gravado.Id, gravado.CorrelationId, gravado.OrcamentoId, gravado.OrdemServicoId,
            gravado.Valor, gravado.Status, gravado.CriadoEm, null);
        pagamento.RegistrarCobranca("pref-2", "https://mp.test/2");
        var dynamo = new Mock<IAmazonDynamoDB>();
        dynamo.Setup(client => client.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConditionalCheckFailedException("condicao"));
        dynamo.Setup(client => client.GetItemAsync(It.IsAny<GetItemRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetItemResponse { Item = Mapear(gravado) });

        var result = await CriarRepository(dynamo).RegistrarCobrancaAsync(pagamento, CancellationToken.None);

        result.CobrancaId.Should().Be("pref-1");
        result.LinkPagamento.Should().Be("https://mp.test/1");
    }

    [Fact]
    public async Task ObterPorOrdem_DeveDevolverATentativaMaisRecente()
    {
        var correlationId = Guid.NewGuid();
        var orcamentoId = Guid.NewGuid();
        var primeira = Pagamento.Criar(correlationId, orcamentoId, 3007, 219.90m, DateTimeOffset.UtcNow);
        primeira.Recusar(DateTimeOffset.UtcNow, "cancelled");
        var segunda = Pagamento.Criar(correlationId, orcamentoId, 3007, 219.90m, DateTimeOffset.UtcNow, 2);
        var itemSegunda = MapearBasico(segunda);
        itemSegunda["Tentativa"] = new() { N = "2" };
        var dynamo = new Mock<IAmazonDynamoDB>();
        dynamo.Setup(client => client.QueryAsync(It.IsAny<QueryRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueryResponse { Items = [itemSegunda, Mapear(primeira)] });

        var result = await CriarRepository(dynamo).ObterPorOrdemServicoIdAsync(3007, CancellationToken.None);

        result!.Id.Should().Be(segunda.Id);
        result.Tentativa.Should().Be(2);
        result.Status.Should().Be("Pendente");
    }

    [Fact]
    public async Task ObterPorOrdem_DeveTratarItemSemTentativaComoPrimeira()
    {
        var pagamento = CriarPagamento();
        var dynamo = new Mock<IAmazonDynamoDB>();
        dynamo.Setup(client => client.QueryAsync(It.IsAny<QueryRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueryResponse { Items = [MapearBasico(pagamento)] });

        var result = await CriarRepository(dynamo).ObterPorOrdemServicoIdAsync(3007, CancellationToken.None);

        result!.Tentativa.Should().Be(1);
    }

    [Fact]
    public async Task ObterPorOrdem_DeveRetornarNuloSemPagamento()
    {
        var dynamo = new Mock<IAmazonDynamoDB>();
        dynamo.Setup(client => client.QueryAsync(It.IsAny<QueryRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueryResponse { Items = [] });

        var result = await CriarRepository(dynamo).ObterPorOrdemServicoIdAsync(3007, CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Criar_DeveGravarTentativaEDevolverExistenteNaReentrega()
    {
        var pagamento = Pagamento.Criar(Guid.NewGuid(), Guid.NewGuid(), 3007, 219.90m, DateTimeOffset.UtcNow, 2);
        TransactWriteItemsRequest? request = null;
        GetItemRequest? leitura = null;
        var dynamo = new Mock<IAmazonDynamoDB>();
        dynamo.Setup(client => client.TransactWriteItemsAsync(
                It.IsAny<TransactWriteItemsRequest>(), It.IsAny<CancellationToken>()))
            .Callback<TransactWriteItemsRequest, CancellationToken>((value, _) => request = value)
            .ThrowsAsync(new TransactionCanceledException("condicao"));
        dynamo.Setup(client => client.GetItemAsync(It.IsAny<GetItemRequest>(), It.IsAny<CancellationToken>()))
            .Callback<GetItemRequest, CancellationToken>((value, _) => leitura = value)
            .ReturnsAsync(new GetItemResponse { Item = Mapear(pagamento) });

        var result = await CriarRepository(dynamo).CriarOuObterAsync(
            pagamento, Guid.NewGuid(), CancellationToken.None);

        result.Criado.Should().BeFalse();
        result.Pagamento.Id.Should().Be(pagamento.Id);
        request!.TransactItems[0].Put.Item["Tentativa"].N.Should().Be("2");
        leitura!.Key["SK"].S.Should().Be($"PAGAMENTO#{pagamento.Id}");
    }

    private static DynamoDbPagamentoRepository CriarRepository(Mock<IAmazonDynamoDB> dynamo) =>
        new(dynamo.Object, Options.Create(new DynamoDbOptions { TableName = "oficina-billing-test" }));

    private static Pagamento CriarPagamento() => Pagamento.Criar(
        Guid.NewGuid(), Guid.NewGuid(), 3007, 219.90m, DateTimeOffset.UtcNow);

    private static Dictionary<string, AttributeValue> Mapear(Pagamento pagamento)
    {
        var item = MapearBasico(pagamento);
        if (pagamento.ConfirmadoEm.HasValue)
            item["ConfirmadoEm"] = new(pagamento.ConfirmadoEm.Value.ToString("O", CultureInfo.InvariantCulture));
        if (pagamento.CobrancaId is not null)
            item["CobrancaId"] = new(pagamento.CobrancaId);
        if (pagamento.LinkPagamento is not null)
            item["LinkPagamento"] = new(pagamento.LinkPagamento);
        if (pagamento.MercadoPagoPagamentoId is not null)
            item["MercadoPagoPagamentoId"] = new(pagamento.MercadoPagoPagamentoId);
        return item;
    }

    private static Dictionary<string, AttributeValue> MapearBasico(Pagamento pagamento) => new()
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
