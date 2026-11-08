namespace Fiap.TechChallenge.OficinaMecanica.Billing.Infrastructure.Persistence;

public sealed class DynamoDbOptions
{
    public const string SectionName = "DynamoDb";

    public string Region { get; init; } = "us-east-1";
    public string? ServiceUrl { get; init; }
    public string TableName { get; init; } = "oficina-billing";
    public bool CreateTableIfMissing { get; init; }
}
