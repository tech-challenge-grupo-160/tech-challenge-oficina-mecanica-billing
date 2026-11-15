using System.Diagnostics.CodeAnalysis;
using Amazon.SQS;
using Fiap.TechChallenge.OficinaMecanica.Billing.Application.Messaging;
using Fiap.TechChallenge.OficinaMecanica.Billing.Application.Orcamentos;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fiap.TechChallenge.OficinaMecanica.Billing.Infrastructure.Messaging;

[ExcludeFromCodeCoverage]
public sealed class GerarOrcamentoConsumerWorker(
    IServiceScopeFactory scopeFactory,
    IAmazonSQS sqs,
    IOptions<MessagingOptions> options,
    ILogger<GerarOrcamentoConsumerWorker> logger)
    : SqsCommandConsumerWorker<GerarOrcamentoMessage>(scopeFactory, sqs, logger)
{
    protected override bool Enabled => options.Value.Enabled;
    protected override string QueueUrl => options.Value.GerarOrcamentoQueueUrl;

    protected override Task ProcessarComandoAsync(
        GerarOrcamentoMessage comando,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        if (comando.Versao != 1)
            throw new InvalidOperationException($"Versao de comando nao suportada: {comando.Versao}.");

        var sender = serviceProvider.GetRequiredService<ISender>();
        return sender.Send(new GerarOrcamentoCommand(
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
    }
}
