using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
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
                    VisibilityTimeout = 30
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
        try
        {
            var comando = JsonSerializer.Deserialize<TMessage>(message.Body, JsonOptions)
                ?? throw new InvalidOperationException($"Comando {typeof(TMessage).Name} invalido.");

            await using var scope = scopeFactory.CreateAsyncScope();
            await ProcessarComandoAsync(comando, scope.ServiceProvider, cancellationToken);
            await sqs.DeleteMessageAsync(QueueUrl, message.ReceiptHandle, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Falha ao processar comando {TipoMensagem} com ID SQS {SqsMessageId}.",
                typeof(TMessage).Name,
                message.MessageId);
        }
    }

    protected abstract Task ProcessarComandoAsync(
        TMessage comando,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken);
}
