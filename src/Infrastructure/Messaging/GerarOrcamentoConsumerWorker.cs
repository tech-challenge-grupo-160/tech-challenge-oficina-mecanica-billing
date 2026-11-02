using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using Fiap.TechChallenge.OficinaMecanica.Billing.Application.Messaging;
using Fiap.TechChallenge.OficinaMecanica.Billing.Application.Orcamentos;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fiap.TechChallenge.OficinaMecanica.Billing.Infrastructure.Messaging;

[ExcludeFromCodeCoverage]
public sealed class GerarOrcamentoConsumerWorker(
    IServiceScopeFactory scopeFactory,
    IAmazonSQS sqs,
    IOptions<MessagingOptions> options,
    ILogger<GerarOrcamentoConsumerWorker> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
            return;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var response = await sqs.ReceiveMessageAsync(new ReceiveMessageRequest
                {
                    QueueUrl = options.Value.GerarOrcamentoQueueUrl,
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
                logger.LogError(exception, "Falha ao consumir comandos de geracao de orcamento.");
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
        }
    }

    private async Task ProcessarAsync(Message message, CancellationToken cancellationToken)
    {
        try
        {
            var comando = JsonSerializer.Deserialize<GerarOrcamentoMessage>(message.Body, JsonOptions)
                ?? throw new InvalidOperationException("Comando GerarOrcamento invalido.");
            if (comando.Versao != 1)
                throw new InvalidOperationException($"Versao de comando nao suportada: {comando.Versao}.");

            await using var scope = scopeFactory.CreateAsyncScope();
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new GerarOrcamentoCommand(
                comando.MessageId,
                comando.CorrelationId,
                comando.OrdemServicoId,
                comando.DiagnosticoId,
                comando.ParecerTecnico,
                comando.Servicos.Select(item => new ServicoOrcamentoCommand(
                    item.ServicoId,
                    item.Nome,
                    item.Preco,
                    item.TempoEstimado)).ToArray(),
                comando.Pecas.Select(item => new PecaOrcamentoCommand(
                    item.PecaId,
                    item.Nome,
                    item.Quantidade,
                    item.Preco)).ToArray(),
                comando.DiagnosticoConcluidoEm), cancellationToken);

            await sqs.DeleteMessageAsync(
                options.Value.GerarOrcamentoQueueUrl,
                message.ReceiptHandle,
                cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Falha ao processar comando SQS {SqsMessageId}.", message.MessageId);
        }
    }
}
