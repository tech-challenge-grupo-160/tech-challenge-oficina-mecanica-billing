using System.Diagnostics.CodeAnalysis;
using Amazon.SQS;
using Fiap.TechChallenge.OficinaMecanica.Billing.Application.Messaging;
using Fiap.TechChallenge.OficinaMecanica.Billing.Application.Pagamentos;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fiap.TechChallenge.OficinaMecanica.Billing.Infrastructure.Messaging;

[ExcludeFromCodeCoverage]
public sealed class SolicitarPagamentoConsumerWorker(
    IServiceScopeFactory scopeFactory,
    IAmazonSQS sqs,
    IOptions<MessagingOptions> options,
    ILogger<SolicitarPagamentoConsumerWorker> logger)
    : SqsCommandConsumerWorker<SolicitarPagamentoMessage>(scopeFactory, sqs, logger)
{
    protected override bool Enabled => options.Value.Enabled;
    protected override string QueueUrl => options.Value.SolicitarPagamentoQueueUrl;

    protected override Task ProcessarComandoAsync(
        SolicitarPagamentoMessage comando,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        if (comando.Versao != 1)
            throw new InvalidOperationException($"Versao de comando nao suportada: {comando.Versao}.");

        var sender = serviceProvider.GetRequiredService<ISender>();
        return sender.Send(new SolicitarPagamentoCommand(
            comando.MessageId,
            comando.CorrelationId,
            comando.OrcamentoId,
            comando.OrdemServicoId,
            comando.OcorridoEm), cancellationToken);
    }
}
