using System.Diagnostics.CodeAnalysis;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Fiap.TechChallenge.OficinaMecanica.Billing.Infrastructure.Persistence;

[ExcludeFromCodeCoverage]
public sealed class DynamoDbTableInitializer(
    IAmazonDynamoDB dynamoDb,
    IOptions<DynamoDbOptions> options) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!options.Value.CreateTableIfMissing)
            return;

        var tabelas = await dynamoDb.ListTablesAsync(cancellationToken);
        if (tabelas.TableNames.Contains(options.Value.TableName, StringComparer.Ordinal))
            return;

        await dynamoDb.CreateTableAsync(new CreateTableRequest
        {
            TableName = options.Value.TableName,
            BillingMode = BillingMode.PAY_PER_REQUEST,
            AttributeDefinitions =
            [
                new AttributeDefinition("PK", ScalarAttributeType.S),
                new AttributeDefinition("SK", ScalarAttributeType.S)
            ],
            KeySchema =
            [
                new KeySchemaElement("PK", KeyType.HASH),
                new KeySchemaElement("SK", KeyType.RANGE)
            ]
        }, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
