namespace Fiap.TechChallenge.OficinaMecanica.Billing.Infrastructure.Messaging;

public sealed class MessagingOptions
{
    public const string SectionName = "Messaging";

    public bool Enabled { get; init; }
    public string Region { get; init; } = "us-east-1";
    public string? ServiceUrl { get; init; }
    public string DiagnosticoConcluidoQueueUrl { get; init; } = string.Empty;
    public string EventosTopicArn { get; init; } = string.Empty;
}
