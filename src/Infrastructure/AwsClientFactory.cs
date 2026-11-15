using System.Diagnostics.CodeAnalysis;
using Amazon;
using Amazon.DynamoDBv2;
using Amazon.Runtime.Credentials;
using Amazon.SimpleNotificationService;
using Amazon.SQS;
using Fiap.TechChallenge.OficinaMecanica.Billing.Infrastructure.Messaging;
using Fiap.TechChallenge.OficinaMecanica.Billing.Infrastructure.Persistence;

namespace Fiap.TechChallenge.OficinaMecanica.Billing.Infrastructure;

[ExcludeFromCodeCoverage]
internal static class AwsClientFactory
{
    public static IAmazonDynamoDB CreateDynamoDb(DynamoDbOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.ServiceUrl))
        {
            var config = new AmazonDynamoDBConfig
            {
                ServiceURL = options.ServiceUrl,
                AuthenticationRegion = options.Region
            };
            var credentials = DefaultAWSCredentialsIdentityResolver.GetCredentials(config);
            return new AmazonDynamoDBClient(credentials, config);
        }

        return new AmazonDynamoDBClient(RegionEndpoint.GetBySystemName(options.Region));
    }

    public static IAmazonSQS CreateSqs(MessagingOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.ServiceUrl))
        {
            var config = new AmazonSQSConfig
            {
                ServiceURL = options.ServiceUrl,
                AuthenticationRegion = options.Region
            };
            var credentials = DefaultAWSCredentialsIdentityResolver.GetCredentials(config);
            return new AmazonSQSClient(credentials, config);
        }

        return new AmazonSQSClient(RegionEndpoint.GetBySystemName(options.Region));
    }

    public static IAmazonSimpleNotificationService CreateSns(MessagingOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.ServiceUrl))
        {
            var config = new AmazonSimpleNotificationServiceConfig
            {
                ServiceURL = options.ServiceUrl,
                AuthenticationRegion = options.Region
            };
            var credentials = DefaultAWSCredentialsIdentityResolver.GetCredentials(config);
            return new AmazonSimpleNotificationServiceClient(credentials, config);
        }

        return new AmazonSimpleNotificationServiceClient(
            RegionEndpoint.GetBySystemName(options.Region));
    }
}
