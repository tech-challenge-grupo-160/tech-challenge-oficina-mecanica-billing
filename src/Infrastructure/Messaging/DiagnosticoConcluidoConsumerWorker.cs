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
public sealed class DiagnosticoConcluidoConsumerWorker(
    IServiceScopeFactory scopeFactory,
    IAmazonSQS sqs,
    IOptions<MessagingOptions> options,
    ILogger<DiagnosticoConcluidoConsumerWorker> logger) : BackgroundService
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
                    QueueUrl = options.Value.DiagnosticoConcluidoQueueUrl,
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
                logger.LogError(exception, "Falha ao consumir diagnosticos concluidos.");
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
        }
    }

    private async Task ProcessarAsync(Message message, CancellationToken cancellationToken)
    {
        try
        {
            var evento = JsonSerializer.Deserialize<DiagnosticoConcluidoMessage>(message.Body, JsonOptions)
                ?? throw new InvalidOperationException("Evento DiagnosticoConcluido invalido.");
            if (evento.Versao != 1)
                throw new InvalidOperationException($"Versao de evento nao suportada: {evento.Versao}.");
            if (!string.Equals(evento.Status, "Concluido", StringComparison.Ordinal))
                throw new InvalidOperationException($"Status de diagnostico invalido: {evento.Status}.");

            await using var scope = scopeFactory.CreateAsyncScope();
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new GerarOrcamentoCommand(
                evento.MessageId,
                evento.CorrelationId,
                evento.OrdemServicoId,
                evento.DiagnosticoId,
                evento.ParecerTecnico,
                evento.Servicos.Select(item => new ServicoOrcamentoCommand(
                    item.ServicoId,
                    item.Nome,
                    item.Preco,
                    item.TempoEstimado)).ToArray(),
                evento.Pecas.Select(item => new PecaOrcamentoCommand(
                    item.PecaId,
                    item.Nome,
                    item.Quantidade,
                    item.Preco)).ToArray(),
                evento.ConcluidoEm), cancellationToken);

            await sqs.DeleteMessageAsync(
                options.Value.DiagnosticoConcluidoQueueUrl,
                message.ReceiptHandle,
                cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Falha ao processar mensagem SQS {SqsMessageId}.", message.MessageId);
        }
    }
}
