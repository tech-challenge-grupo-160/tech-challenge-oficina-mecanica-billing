using System.Globalization;
using System.Text.Json;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Fiap.TechChallenge.OficinaMecanica.Billing.Application.Abstractions;
using Fiap.TechChallenge.OficinaMecanica.Billing.Application.Messaging;
using Fiap.TechChallenge.OficinaMecanica.Billing.Domain.Entities;
using Microsoft.Extensions.Options;

namespace Fiap.TechChallenge.OficinaMecanica.Billing.Infrastructure.Persistence;

public sealed class DynamoDbOrcamentoRepository(
    IAmazonDynamoDB dynamoDb,
    IOptions<DynamoDbOptions> options) : IOrcamentoRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<(Orcamento Orcamento, bool Criado)> CriarOuObterAsync(
        Orcamento candidato,
        Guid mensagemEntradaId,
        OrcamentoGeradoMessage eventoSaida,
        CancellationToken cancellationToken)
    {
        var transacao = new TransactWriteItemsRequest
        {
            TransactItems =
            [
                new TransactWriteItem
                {
                    Put = new Put
                    {
                        TableName = options.Value.TableName,
                        Item = MapearOrcamento(candidato),
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
                            nameof(GerarOrcamentoMessage)),
                        ConditionExpression = "attribute_not_exists(PK)"
                    }
                },
                new TransactWriteItem
                {
                    Put = new Put
                    {
                        TableName = options.Value.TableName,
                        Item = DynamoDbMessageItemFactory.CriarOutbox(
                            eventoSaida.MessageId,
                            eventoSaida.CorrelationId,
                            nameof(OrcamentoGeradoMessage),
                            JsonSerializer.Serialize(eventoSaida, JsonOptions),
                            eventoSaida.OcorridoEm),
                        ConditionExpression = "attribute_not_exists(PK)"
                    }
                }
            ]
        };

        try
        {
            await dynamoDb.TransactWriteItemsAsync(transacao, cancellationToken);
            return (candidato, true);
        }
        catch (TransactionCanceledException)
        {
            var existente = await ObterPorOrdemServicoIdAsync(
                candidato.OrdemServicoId,
                cancellationToken);
            if (existente is not null && existente.CorrespondeAoDiagnostico(
                    candidato.CorrelationId,
                    candidato.DiagnosticoId))
                return (existente, false);

            throw new InvalidOperationException(
                "A ordem de servico ja possui um orcamento incompatível.");
        }
    }

    public async Task<Orcamento?> ObterPorOrdemServicoIdAsync(
        int ordemServicoId,
        CancellationToken cancellationToken)
    {
        var response = await dynamoDb.GetItemAsync(new GetItemRequest
        {
            TableName = options.Value.TableName,
            ConsistentRead = true,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"ORCAMENTO#{ordemServicoId}"),
                ["SK"] = new("ORCAMENTO")
            }
        }, cancellationToken);

        return response.IsItemSet ? Restaurar(response.Item) : null;
    }

    public async Task<(Orcamento Orcamento, bool Alterado)> RegistrarDecisaoAsync(
        Orcamento orcamento,
        Guid eventoId,
        string tipoEvento,
        string payloadEvento,
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
                        Item = MapearOrcamento(orcamento),
                        ConditionExpression = "#status = :gerado AND Id = :id",
                        ExpressionAttributeNames = new Dictionary<string, string>
                        {
                            ["#status"] = "Status"
                        },
                        ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                        {
                            [":gerado"] = new("Gerado"),
                            [":id"] = new(orcamento.Id.ToString())
                        }
                    }
                },
                new TransactWriteItem
                {
                    Put = new Put
                    {
                        TableName = options.Value.TableName,
                        Item = DynamoDbMessageItemFactory.CriarOutbox(
                            eventoId,
                            orcamento.CorrelationId,
                            tipoEvento,
                            payloadEvento,
                            orcamento.RespondidoEm ?? DateTimeOffset.UtcNow),
                        ConditionExpression = "attribute_not_exists(PK)"
                    }
                }
            ]
        };

        try
        {
            await dynamoDb.TransactWriteItemsAsync(request, cancellationToken);
            return (orcamento, true);
        }
        catch (TransactionCanceledException)
        {
            var existente = await ObterPorOrdemServicoIdAsync(
                orcamento.OrdemServicoId,
                cancellationToken);
            if (existente is not null &&
                existente.Id == orcamento.Id &&
                string.Equals(existente.Status, orcamento.Status, StringComparison.Ordinal))
                return (existente, false);

            throw new InvalidOperationException(
                "O orcamento ja recebeu uma decisao diferente ou foi alterado.");
        }
    }

    internal static Dictionary<string, AttributeValue> MapearOrcamento(Orcamento orcamento)
    {
        var item = new Dictionary<string, AttributeValue>
        {
            ["PK"] = new($"ORCAMENTO#{orcamento.OrdemServicoId}"),
            ["SK"] = new("ORCAMENTO"),
            ["EntityType"] = new("Orcamento"),
            ["Id"] = new(orcamento.Id.ToString()),
            ["CorrelationId"] = new(orcamento.CorrelationId.ToString()),
            ["OrdemServicoId"] = new() { N = orcamento.OrdemServicoId.ToString(CultureInfo.InvariantCulture) },
            ["DiagnosticoId"] = new() { N = orcamento.DiagnosticoId.ToString(CultureInfo.InvariantCulture) },
            ["ParecerTecnico"] = new(orcamento.ParecerTecnico),
            ["Status"] = new(orcamento.Status),
            ["CriadoEm"] = new(orcamento.CriadoEm.ToString("O", CultureInfo.InvariantCulture)),
            ["ValorServicos"] = new() { N = orcamento.ValorServicos.ToString(CultureInfo.InvariantCulture) },
            ["ValorPecas"] = new() { N = orcamento.ValorPecas.ToString(CultureInfo.InvariantCulture) },
            ["ValorTotal"] = new() { N = orcamento.ValorTotal.ToString(CultureInfo.InvariantCulture) },
            ["Servicos"] = new(JsonSerializer.Serialize(orcamento.Servicos, JsonOptions)),
            ["Pecas"] = new(JsonSerializer.Serialize(orcamento.Pecas, JsonOptions))
        };
        if (orcamento.RespondidoEm.HasValue)
            item["RespondidoEm"] = new(orcamento.RespondidoEm.Value.ToString("O", CultureInfo.InvariantCulture));
        if (!string.IsNullOrWhiteSpace(orcamento.MotivoRecusa))
            item["MotivoRecusa"] = new(orcamento.MotivoRecusa);

        return item;
    }

    private static Orcamento Restaurar(IReadOnlyDictionary<string, AttributeValue> item) =>
        Orcamento.Restaurar(
            Guid.Parse(item["Id"].S),
            Guid.Parse(item["CorrelationId"].S),
            int.Parse(item["OrdemServicoId"].N, CultureInfo.InvariantCulture),
            int.Parse(item["DiagnosticoId"].N, CultureInfo.InvariantCulture),
            item["ParecerTecnico"].S,
            item["Status"].S,
            DateTimeOffset.Parse(item["CriadoEm"].S, CultureInfo.InvariantCulture),
            JsonSerializer.Deserialize<ItemServicoOrcamento[]>(item["Servicos"].S, JsonOptions) ?? [],
            JsonSerializer.Deserialize<ItemPecaOrcamento[]>(item["Pecas"].S, JsonOptions) ?? [],
            item.TryGetValue("RespondidoEm", out var respondidoEm)
                ? DateTimeOffset.Parse(respondidoEm.S, CultureInfo.InvariantCulture)
                : null,
            item.TryGetValue("MotivoRecusa", out var motivoRecusa) ? motivoRecusa.S : null);
}
