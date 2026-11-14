using System.Diagnostics.CodeAnalysis;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Fiap.TechChallenge.OficinaMecanica.Billing.Infrastructure.Logging;
using Fiap.TechChallenge.OficinaMecanica.Billing.Infrastructure.Observability;
using Fiap.TechChallenge.OficinaMecanica.Billing.Infrastructure.Persistence;
using Fiap.TechChallenge.OficinaMecanica.Billing.Shared.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fiap.TechChallenge.OficinaMecanica.Billing.Infrastructure.Messaging;

[ExcludeFromCodeCoverage]
public sealed class OutboxSnsPublisherWorker(
    IAmazonDynamoDB dynamoDb,
    IAmazonSimpleNotificationService sns,
    IOptions<DynamoDbOptions> dynamoOptions,
    IOptions<MessagingOptions> messagingOptions,
    ILogger<OutboxSnsPublisherWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!messagingOptions.Value.Enabled)
            return;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PublicarPendentesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Falha ao publicar a Outbox do Billing.");
            }

            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }
    }

    private async Task PublicarPendentesAsync(CancellationToken cancellationToken)
    {
        var pendentes = new List<Dictionary<string, AttributeValue>>();
        Dictionary<string, AttributeValue>? ultimaChave = null;

        do
        {
            var request = new ScanRequest
            {
                TableName = dynamoOptions.Value.TableName,
                FilterExpression = "EntityType = :tipo AND attribute_not_exists(PublicadoEm)",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":tipo"] = new("Outbox")
                }
            };
            if (ultimaChave is not null)
                request.ExclusiveStartKey = ultimaChave;

            var response = await dynamoDb.ScanAsync(request, cancellationToken);
            pendentes.AddRange(response.Items.Take(10 - pendentes.Count));
            ultimaChave = response.LastEvaluatedKey is { Count: > 0 }
                ? response.LastEvaluatedKey
                : null;
        }
        while (pendentes.Count < 10 && ultimaChave is not null);

        foreach (var item in pendentes)
            await PublicarAsync(item, cancellationToken);
    }

    private async Task PublicarAsync(
        Dictionary<string, AttributeValue> item,
        CancellationToken cancellationToken)
    {
        var correlationId = LerGuid(item, "CorrelationId");
        var ordemServicoId = OutboxLogContexto.LerOrdemServicoId(item["Payload"].S);
        // A publicacao continua o trace de quem gravou o evento; se foi uma
        // requisicao HTTP (aprovacao, webhook), o trace da Saga (F4-31).
        var contextoDaSaga = correlationId is null
            ? null
            : await ObterContextoDaSagaAsync(correlationId.Value, cancellationToken);
        using var span = MensageriaTrace.IniciarPublicacao(
            MensageriaTrace.EscolherContextoPai(
                item.TryGetValue(DynamoDbMessageItemFactory.TraceContext, out var traceContext) ? traceContext.S : null,
                contextoDaSaga),
            item["Tipo"].S,
            MensageriaTrace.SistemaSns,
            item.TryGetValue("Destino", out var destino) ? destino.S : "OficinaEventos",
            correlationId ?? Guid.Empty,
            ordemServicoId);
        using var contexto = logger.Iniciar(
            correlationId,
            ordemServicoId,
            LerGuid(item, "MessageId"),
            item["Tipo"].S);

        try
        {
            var atributos = new Dictionary<string, MessageAttributeValue>
            {
                ["tipo"] = new() { DataType = "String", StringValue = item["Tipo"].S }
            };
            if (MensageriaTrace.CapturarContextoAtual() is { } contextoDaPublicacao)
                atributos[MensageriaTrace.AtributoMensagem] = new() { DataType = "String", StringValue = contextoDaPublicacao };

            await sns.PublishAsync(new PublishRequest
            {
                TopicArn = messagingOptions.Value.EventosTopicArn,
                Message = item["Payload"].S,
                MessageAttributes = atributos
            }, cancellationToken);

            await dynamoDb.UpdateItemAsync(new UpdateItemRequest
            {
                TableName = dynamoOptions.Value.TableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    ["PK"] = item["PK"],
                    ["SK"] = item["SK"]
                },
                UpdateExpression = "SET PublicadoEm = :agora REMOVE UltimoErro",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":agora"] = new(DateTimeOffset.UtcNow.ToString("O"))
                }
            }, cancellationToken);
            logger.LogInformation("Evento {TipoMensagem} publicado.", item["Tipo"].S);
        }
        catch (Exception exception)
        {
            span.Span.SetException(exception);
            await dynamoDb.UpdateItemAsync(new UpdateItemRequest
            {
                TableName = dynamoOptions.Value.TableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    ["PK"] = item["PK"],
                    ["SK"] = item["SK"]
                },
                UpdateExpression = "SET Tentativas = Tentativas + :um, UltimoErro = :erro",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":um"] = new() { N = "1" },
                    [":erro"] = new(exception.Message[..Math.Min(exception.Message.Length, 2000)])
                }
            }, cancellationToken);
            logger.LogWarning(exception, "Falha ao publicar evento {MessageId}.", item["MessageId"].S);
        }
    }

    /// <summary>
    /// Trace da Saga: o contexto mais recente gravado na Inbox para o
    /// correlationId. A tabela e pequena e o worker ja varre a tabela a cada
    /// segundo; um indice por CorrelationId so compensaria com muito volume.
    /// </summary>
    private async Task<string?> ObterContextoDaSagaAsync(Guid correlationId, CancellationToken cancellationToken)
    {
        string? contexto = null;
        string? processadoEm = null;
        Dictionary<string, AttributeValue>? ultimaChave = null;

        do
        {
            var request = new ScanRequest
            {
                TableName = dynamoOptions.Value.TableName,
                FilterExpression = "EntityType = :tipo AND CorrelationId = :correlationId AND attribute_exists(#trace)",
                ExpressionAttributeNames = new Dictionary<string, string>
                {
                    ["#trace"] = DynamoDbMessageItemFactory.TraceContext
                },
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":tipo"] = new("Inbox"),
                    [":correlationId"] = new(correlationId.ToString())
                }
            };
            if (ultimaChave is not null)
                request.ExclusiveStartKey = ultimaChave;

            var response = await dynamoDb.ScanAsync(request, cancellationToken);
            foreach (var item in response.Items)
            {
                var data = item.TryGetValue("ProcessadoEm", out var valor) ? valor.S : null;
                if (contexto is null || string.CompareOrdinal(data, processadoEm) > 0)
                {
                    contexto = item[DynamoDbMessageItemFactory.TraceContext].S;
                    processadoEm = data;
                }
            }

            ultimaChave = response.LastEvaluatedKey is { Count: > 0 }
                ? response.LastEvaluatedKey
                : null;
        }
        while (ultimaChave is not null);

        return contexto;
    }

    private static Guid? LerGuid(Dictionary<string, AttributeValue> item, string nome) =>
        item.TryGetValue(nome, out var valor) && Guid.TryParse(valor.S, out var guid) ? guid : null;
}
