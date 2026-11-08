using System.Globalization;
using System.Text.Json;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Fiap.TechChallenge.OficinaMecanica.Billing.Application.Abstractions;
using Fiap.TechChallenge.OficinaMecanica.Billing.Application.Messaging;
using Fiap.TechChallenge.OficinaMecanica.Billing.Domain.Entities;
using Microsoft.Extensions.Options;

namespace Fiap.TechChallenge.OficinaMecanica.Billing.Infrastructure.Persistence;

public sealed class DynamoDbPagamentoRepository(
    IAmazonDynamoDB dynamoDb,
    IOptions<DynamoDbOptions> options) : IPagamentoRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<(Pagamento Pagamento, bool Criado)> CriarOuObterAsync(
        Pagamento candidato,
        Guid mensagemEntradaId,
        CancellationToken cancellationToken)
    {
        var request = new TransactWriteItemsRequest
        {
            TransactItems =
            [
                new TransactWriteItem
                {
                    Put = new Put
                    {
                        TableName = options.Value.TableName,
                        Item = MapearPagamento(candidato),
                        ConditionExpression = "attribute_not_exists(PK)"
                    }
                },
                new TransactWriteItem
                {
                    Put = new Put
                    {
                        TableName = options.Value.TableName,
                        Item = DynamoDbMessageItemFactory.CriarInbox(
                            mensagemEntradaId,
                            candidato.CorrelationId,
                            nameof(SolicitarPagamentoMessage)),
                        ConditionExpression = "attribute_not_exists(PK)"
                    }
                }
            ]
        };

        try
        {
            await dynamoDb.TransactWriteItemsAsync(request, cancellationToken);
            return (candidato, true);
        }
        catch (TransactionCanceledException)
        {
            var existente = await ObterPorOrdemServicoIdAsync(
                candidato.OrdemServicoId,
                cancellationToken);
            if (existente is not null && existente.Corresponde(
                    candidato.CorrelationId,
                    candidato.OrcamentoId))
                return (existente, false);

            throw new InvalidOperationException("A ordem de servico ja possui pagamento incompativel.");
        }
    }

    public async Task<Pagamento?> ObterPorOrdemServicoIdAsync(
        int ordemServicoId,
        CancellationToken cancellationToken)
    {
        var response = await dynamoDb.QueryAsync(new QueryRequest
        {
            TableName = options.Value.TableName,
            ConsistentRead = true,
            KeyConditionExpression = "PK = :pk AND begins_with(SK, :prefixo)",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new(ObterPk(ordemServicoId)),
                [":prefixo"] = new("PAGAMENTO#")
            },
            Limit = 1
        }, cancellationToken);

        return response.Items.Count == 0 ? null : Restaurar(response.Items[0]);
    }

    public async Task<Pagamento?> ObterAsync(
        int ordemServicoId,
        Guid pagamentoId,
        CancellationToken cancellationToken)
    {
        var response = await dynamoDb.GetItemAsync(new GetItemRequest
        {
            TableName = options.Value.TableName,
            ConsistentRead = true,
            Key = ObterChave(ordemServicoId, pagamentoId)
        }, cancellationToken);

        return response.IsItemSet ? Restaurar(response.Item) : null;
    }

    public async Task<(Pagamento Pagamento, bool Alterado)> ConfirmarAsync(
        Pagamento pagamento,
        PagamentoConfirmadoMessage evento,
        CancellationToken cancellationToken)
    {
        var request = new TransactWriteItemsRequest
        {
            TransactItems =
            [
                new TransactWriteItem
                {
                    Put = new Put
                    {
                        TableName = options.Value.TableName,
                        Item = MapearPagamento(pagamento),
                        ConditionExpression = "#status = :pendente AND Id = :id",
                        ExpressionAttributeNames = new Dictionary<string, string>
                        {
                            ["#status"] = "Status"
                        },
                        ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                        {
                            [":pendente"] = new("Pendente"),
                            [":id"] = new(pagamento.Id.ToString())
                        }
                    }
                },
                new TransactWriteItem
                {
                    Put = new Put
                    {
                        TableName = options.Value.TableName,
                        Item = DynamoDbMessageItemFactory.CriarOutbox(
                            evento.MessageId,
                            evento.CorrelationId,
                            nameof(PagamentoConfirmadoMessage),
                            JsonSerializer.Serialize(evento, JsonOptions),
                            evento.OcorridoEm),
                        ConditionExpression = "attribute_not_exists(PK)"
                    }
                }
            ]
        };

        try
        {
            await dynamoDb.TransactWriteItemsAsync(request, cancellationToken);
            return (pagamento, true);
        }
        catch (TransactionCanceledException)
        {
            var existente = await ObterAsync(
                pagamento.OrdemServicoId,
                pagamento.Id,
                cancellationToken);
            if (existente?.Status == "Confirmado")
                return (existente, false);

            throw new InvalidOperationException("Pagamento foi alterado durante a confirmacao.");
        }
    }

    internal static Dictionary<string, AttributeValue> MapearPagamento(Pagamento pagamento)
    {
        var item = new Dictionary<string, AttributeValue>
        {
            ["PK"] = new(ObterPk(pagamento.OrdemServicoId)),
            ["SK"] = new($"PAGAMENTO#{pagamento.Id}"),
            ["EntityType"] = new("Pagamento"),
            ["Id"] = new(pagamento.Id.ToString()),
            ["CorrelationId"] = new(pagamento.CorrelationId.ToString()),
            ["OrcamentoId"] = new(pagamento.OrcamentoId.ToString()),
            ["OrdemServicoId"] = new() { N = pagamento.OrdemServicoId.ToString(CultureInfo.InvariantCulture) },
            ["Valor"] = new() { N = pagamento.Valor.ToString(CultureInfo.InvariantCulture) },
            ["Status"] = new(pagamento.Status),
            ["CriadoEm"] = new(pagamento.CriadoEm.ToString("O", CultureInfo.InvariantCulture))
        };
        if (pagamento.ConfirmadoEm.HasValue)
            item["ConfirmadoEm"] = new(pagamento.ConfirmadoEm.Value.ToString("O", CultureInfo.InvariantCulture));
        return item;
    }

    private static string ObterPk(int ordemServicoId) => $"PAGAMENTO#OS#{ordemServicoId}";

    private static Dictionary<string, AttributeValue> ObterChave(int ordemServicoId, Guid pagamentoId) => new()
    {
        ["PK"] = new(ObterPk(ordemServicoId)),
        ["SK"] = new($"PAGAMENTO#{pagamentoId}")
    };

    private static Pagamento Restaurar(IReadOnlyDictionary<string, AttributeValue> item) =>
        Pagamento.Restaurar(
            Guid.Parse(item["Id"].S),
            Guid.Parse(item["CorrelationId"].S),
            Guid.Parse(item["OrcamentoId"].S),
            int.Parse(item["OrdemServicoId"].N, CultureInfo.InvariantCulture),
            decimal.Parse(item["Valor"].N, CultureInfo.InvariantCulture),
            item["Status"].S,
            DateTimeOffset.Parse(item["CriadoEm"].S, CultureInfo.InvariantCulture),
            item.TryGetValue("ConfirmadoEm", out var confirmadoEm)
                ? DateTimeOffset.Parse(confirmadoEm.S, CultureInfo.InvariantCulture)
                : null);
}
