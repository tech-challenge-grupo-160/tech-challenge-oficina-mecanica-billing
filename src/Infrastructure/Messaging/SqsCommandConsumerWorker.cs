using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using Fiap.TechChallenge.OficinaMecanica.Billing.Infrastructure.Logging;
using Fiap.TechChallenge.OficinaMecanica.Billing.Infrastructure.Observability;
using Fiap.TechChallenge.OficinaMecanica.Billing.Shared.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fiap.TechChallenge.OficinaMecanica.Billing.Infrastructure.Messaging;

[ExcludeFromCodeCoverage]
public abstract class SqsCommandConsumerWorker<TMessage>(
    IServiceScopeFactory scopeFactory,
    IAmazonSQS sqs,
    ILogger logger) : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    protected abstract bool Enabled { get; }
    protected abstract string QueueUrl { get; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!Enabled)
            return;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var response = await sqs.ReceiveMessageAsync(new ReceiveMessageRequest
                {
                    QueueUrl = QueueUrl,
                    MaxNumberOfMessages = 10,
                    WaitTimeSeconds = 10,
                    VisibilityTimeout = 30,
                    // Contexto do trace de quem publicou (F4-31).
                    MessageAttributeNames = [MensageriaTrace.AtributoMensagem]
                }, stoppingToken);

                foreach (var message in response.Messages ?? [])
                    await ProcessarAsync(message, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Falha ao consumir comandos {TipoMensagem}.", typeof(TMessage).Name);
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
        }
    }

    private async Task ProcessarAsync(Message message, CancellationToken cancellationToken)
    {
        // Processa como filho do trace de quem publicou (F4-31).
        using var span = MensageriaTrace.IniciarConsumo(
            MensageriaTrace.LerAtributo(message.MessageAttributes),
            typeof(TMessage).Name,
            QueueUrl);
        try
        {
            var comando = JsonSerializer.Deserialize<TMessage>(message.Body, JsonOptions)
                ?? throw new InvalidOperationException($"Comando {typeof(TMessage).Name} invalido.");
            MensageriaTrace.IdentificarMensagem(
                span,
                LerCorrelationId(message.Body),
                OutboxLogContexto.LerOrdemServicoId(message.Body));

            // correlationId e numero da OS em todos os logs do processamento.
            using var contexto = logger.IniciarParaMensagem(comando, typeof(TMessage).Name);
            await using var scope = scopeFactory.CreateAsyncScope();
            await ProcessarComandoAsync(comando, scope.ServiceProvider, cancellationToken);
            await sqs.DeleteMessageAsync(QueueUrl, message.ReceiptHandle, cancellationToken);
            logger.LogInformation("Comando {TipoMensagem} processado.", typeof(TMessage).Name);
        }
        catch (Exception exception)
        {
            span.Span.SetException(exception);
            logger.LogError(
                exception,
                "Falha ao processar comando {TipoMensagem} com ID SQS {SqsMessageId}.",
                typeof(TMessage).Name,
                message.MessageId);
        }
    }

    private static Guid? LerCorrelationId(string corpo)
    {
        try
        {
            using var documento = JsonDocument.Parse(corpo);
            return documento.RootElement.ValueKind == JsonValueKind.Object &&
                   documento.RootElement.TryGetProperty("correlationId", out var valor) &&
                   valor.TryGetGuid(out var correlationId)
                ? correlationId
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    protected abstract Task ProcessarComandoAsync(
        TMessage comando,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken);
}
