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
                        Item = MapearInbox(mensagemEntradaId, candidato.CorrelationId),
                        ConditionExpression = "attribute_not_exists(PK)"
                    }
                },
                new TransactWriteItem
                {
                    Put = new Put
                    {
                        TableName = options.Value.TableName,
                        Item = MapearOutbox(eventoSaida),
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

    internal static Dictionary<string, AttributeValue> MapearOrcamento(Orcamento orcamento) => new()
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

    private static Dictionary<string, AttributeValue> MapearInbox(
        Guid messageId,
        Guid correlationId) => new()
        {
            ["PK"] = new($"INBOX#{messageId}"),
            ["SK"] = new("MESSAGE"),
            ["EntityType"] = new("Inbox"),
            ["MessageId"] = new(messageId.ToString()),
            ["CorrelationId"] = new(correlationId.ToString()),
            ["Tipo"] = new(nameof(GerarOrcamentoMessage)),
            ["ProcessadoEm"] = new(DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture))
        };

    private static Dictionary<string, AttributeValue> MapearOutbox(OrcamentoGeradoMessage evento) => new()
    {
        ["PK"] = new($"OUTBOX#{evento.MessageId}"),
        ["SK"] = new("MESSAGE"),
        ["EntityType"] = new("Outbox"),
        ["MessageId"] = new(evento.MessageId.ToString()),
        ["CorrelationId"] = new(evento.CorrelationId.ToString()),
        ["Tipo"] = new(nameof(OrcamentoGeradoMessage)),
        ["Destino"] = new("OficinaEventos"),
        ["Payload"] = new(JsonSerializer.Serialize(evento, JsonOptions)),
        ["CriadoEm"] = new(evento.OcorridoEm.ToString("O", CultureInfo.InvariantCulture)),
        ["Tentativas"] = new() { N = "0" }
    };

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
            JsonSerializer.Deserialize<ItemPecaOrcamento[]>(item["Pecas"].S, JsonOptions) ?? []);
}
