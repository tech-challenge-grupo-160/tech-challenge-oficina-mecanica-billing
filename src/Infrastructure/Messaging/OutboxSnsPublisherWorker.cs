using System.Diagnostics.CodeAnalysis;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Fiap.TechChallenge.OficinaMecanica.Billing.Infrastructure.Logging;
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
        using var contexto = logger.Iniciar(
            LerGuid(item, "CorrelationId"),
            OutboxLogContexto.LerOrdemServicoId(item["Payload"].S),
            LerGuid(item, "MessageId"),
            item["Tipo"].S);

        try
        {
            await sns.PublishAsync(new PublishRequest
            {
                TopicArn = messagingOptions.Value.EventosTopicArn,
                Message = item["Payload"].S,
                MessageAttributes = new Dictionary<string, MessageAttributeValue>
                {
                    ["tipo"] = new() { DataType = "String", StringValue = item["Tipo"].S }
                }
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

    private static Guid? LerGuid(Dictionary<string, AttributeValue> item, string nome) =>
        item.TryGetValue(nome, out var valor) && Guid.TryParse(valor.S, out var guid) ? guid : null;
}
