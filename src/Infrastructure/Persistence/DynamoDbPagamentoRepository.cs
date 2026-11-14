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
            // O Id e deterministico por saga e tentativa (Pagamento.GerarId): a
            // reentrega do comando, ou outro comando para a mesma tentativa, cai
            // no mesmo item.
            var existente = await ObterAsync(
                candidato.OrdemServicoId,
                candidato.Id,
                cancellationToken);
            if (existente is not null && existente.Corresponde(
                    candidato.CorrelationId,
                    candidato.OrcamentoId))
                return (existente, false);

            throw new InvalidOperationException("A ordem de servico ja possui pagamento incompativel.");
        }
    }

    /// <summary>
    /// Devolve o pagamento vigente da OS: o da tentativa mais recente. Depois
    /// de uma recusa a OS pode ter mais de um pagamento (F4-27).
    /// </summary>
    public async Task<Pagamento?> ObterPorOrdemServicoIdAsync(
        int ordemServicoId,
        CancellationToken cancellationToken)
    {
        var pagamentos = new List<Pagamento>();
        Dictionary<string, AttributeValue>? inicio = null;
        do
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
                ExclusiveStartKey = inicio
            }, cancellationToken);

            pagamentos.AddRange((response.Items ?? []).Select(Restaurar));
            inicio = response.LastEvaluatedKey is { Count: > 0 } ? response.LastEvaluatedKey : null;
        }
        while (inicio is not null);

        return pagamentos
            .OrderByDescending(pagamento => pagamento.Tentativa)
            .ThenByDescending(pagamento => pagamento.CriadoEm)
            .FirstOrDefault();
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

    public async Task<Pagamento> RegistrarCobrancaAsync(
        Pagamento pagamento,
        CancellationToken cancellationToken)
    {
        try
        {
            await dynamoDb.UpdateItemAsync(new UpdateItemRequest
            {
                TableName = options.Value.TableName,
                Key = ObterChave(pagamento.OrdemServicoId, pagamento.Id),
                UpdateExpression = "SET CobrancaId = :cobrancaId, LinkPagamento = :link",
                ConditionExpression = "#status = :pendente AND attribute_not_exists(CobrancaId)",
                ExpressionAttributeNames = new Dictionary<string, string>
                {
                    ["#status"] = "Status"
                },
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":cobrancaId"] = new(pagamento.CobrancaId),
                    [":link"] = new(pagamento.LinkPagamento),
                    [":pendente"] = new(Pagamento.StatusPendente)
                }
            }, cancellationToken);
            return pagamento;
        }
        catch (ConditionalCheckFailedException)
        {
            // Outra execucao gravou a cobranca (ou o pagamento ja teve desfecho):
            // vale o que esta persistido.
            return await ObterAsync(pagamento.OrdemServicoId, pagamento.Id, cancellationToken)
                ?? throw new InvalidOperationException("Pagamento removido durante o registro da cobranca.");
        }
    }

    public Task<(Pagamento Pagamento, bool Alterado)> ConfirmarAsync(
        Pagamento pagamento,
        PagamentoConfirmadoMessage evento,
        CancellationToken cancellationToken) =>
        RegistrarDesfechoAsync(
            pagamento,
            evento.MessageId,
            evento.CorrelationId,
            nameof(PagamentoConfirmadoMessage),
            JsonSerializer.Serialize(evento, JsonOptions),
            evento.OcorridoEm,
            cancellationToken);

    public Task<(Pagamento Pagamento, bool Alterado)> RecusarAsync(
        Pagamento pagamento,
        PagamentoRecusadoMessage evento,
        CancellationToken cancellationToken) =>
        RegistrarDesfechoAsync(
            pagamento,
            evento.MessageId,
            evento.CorrelationId,
            nameof(PagamentoRecusadoMessage),
            JsonSerializer.Serialize(evento, JsonOptions),
            evento.OcorridoEm,
            cancellationToken);

    /// <summary>
    /// Grava o desfecho do pagamento e o evento na Outbox na mesma transacao. A
    /// condicao em Pendente garante que notificacoes concorrentes ou repetidas
    /// publiquem o evento uma unica vez.
    /// </summary>
    private async Task<(Pagamento Pagamento, bool Alterado)> RegistrarDesfechoAsync(
        Pagamento pagamento,
        Guid eventoId,
        Guid correlationId,
        string tipoEvento,
        string payload,
        DateTimeOffset ocorridoEm,
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
                            [":pendente"] = new(Pagamento.StatusPendente),
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
                            eventoId,
                            correlationId,
                            tipoEvento,
                            payload,
                            ocorridoEm),
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
            if (existente?.Status == pagamento.Status)
                return (existente, false);

            throw new InvalidOperationException(
                $"Pagamento foi alterado durante o registro do status {pagamento.Status}.");
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
            ["CriadoEm"] = new(pagamento.CriadoEm.ToString("O", CultureInfo.InvariantCulture)),
            ["Tentativa"] = new() { N = pagamento.Tentativa.ToString(CultureInfo.InvariantCulture) }
        };
        if (pagamento.ConfirmadoEm.HasValue)
            item["ConfirmadoEm"] = new(pagamento.ConfirmadoEm.Value.ToString("O", CultureInfo.InvariantCulture));
        if (pagamento.RecusadoEm.HasValue)
            item["RecusadoEm"] = new(pagamento.RecusadoEm.Value.ToString("O", CultureInfo.InvariantCulture));
        AdicionarSePreenchido(item, "MotivoRecusa", pagamento.MotivoRecusa);
        AdicionarSePreenchido(item, "CobrancaId", pagamento.CobrancaId);
        AdicionarSePreenchido(item, "LinkPagamento", pagamento.LinkPagamento);
        AdicionarSePreenchido(item, "MercadoPagoPagamentoId", pagamento.MercadoPagoPagamentoId);
        return item;
    }

    private static void AdicionarSePreenchido(
        Dictionary<string, AttributeValue> item,
        string nome,
        string? valor)
    {
        if (!string.IsNullOrWhiteSpace(valor))
            item[nome] = new(valor);
    }

    private static string? LerTexto(IReadOnlyDictionary<string, AttributeValue> item, string nome) =>
        item.TryGetValue(nome, out var valor) ? valor.S : null;

    private static DateTimeOffset? LerData(IReadOnlyDictionary<string, AttributeValue> item, string nome) =>
        item.TryGetValue(nome, out var valor)
            ? DateTimeOffset.Parse(valor.S, CultureInfo.InvariantCulture)
            : null;

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
            LerData(item, "ConfirmadoEm"),
            LerData(item, "RecusadoEm"),
            LerTexto(item, "MotivoRecusa"),
            LerTexto(item, "CobrancaId"),
            LerTexto(item, "LinkPagamento"),
            LerTexto(item, "MercadoPagoPagamentoId"),
            // Itens gravados antes da F4-27 nao tem Tentativa: eram a primeira.
            item.TryGetValue("Tentativa", out var tentativa)
                ? int.Parse(tentativa.N, CultureInfo.InvariantCulture)
                : 1);
}
