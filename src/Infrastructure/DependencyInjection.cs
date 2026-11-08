using Amazon.DynamoDBv2;
using Amazon.SimpleNotificationService;
using Amazon.SQS;
using Fiap.TechChallenge.OficinaMecanica.Billing.Application.Abstractions;
using Fiap.TechChallenge.OficinaMecanica.Billing.Infrastructure.Messaging;
using Fiap.TechChallenge.OficinaMecanica.Billing.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Fiap.TechChallenge.OficinaMecanica.Billing.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<DynamoDbOptions>(configuration.GetSection(DynamoDbOptions.SectionName));
        services.Configure<MessagingOptions>(configuration.GetSection(MessagingOptions.SectionName));

        services.AddSingleton<IAmazonDynamoDB>(provider =>
            AwsClientFactory.CreateDynamoDb(
                provider.GetRequiredService<IOptions<DynamoDbOptions>>().Value));
        services.AddSingleton<IAmazonSQS>(provider =>
            AwsClientFactory.CreateSqs(
                provider.GetRequiredService<IOptions<MessagingOptions>>().Value));
        services.AddSingleton<IAmazonSimpleNotificationService>(provider =>
            AwsClientFactory.CreateSns(
                provider.GetRequiredService<IOptions<MessagingOptions>>().Value));

        services.AddScoped<IOrcamentoRepository, DynamoDbOrcamentoRepository>();
        services.AddScoped<IPagamentoRepository, DynamoDbPagamentoRepository>();
        services.AddHostedService<DynamoDbTableInitializer>();
        services.AddHostedService<GerarOrcamentoConsumerWorker>();
        services.AddHostedService<SolicitarPagamentoConsumerWorker>();
        services.AddHostedService<OutboxSnsPublisherWorker>();
        services.AddHealthChecks();

        return services;
    }
}
