using System.Globalization;
using Amazon.DynamoDBv2.Model;

namespace Fiap.TechChallenge.OficinaMecanica.Billing.Infrastructure.Persistence;

internal static class DynamoDbMessageItemFactory
{
    public static Dictionary<string, AttributeValue> CriarInbox(
        Guid messageId,
        Guid correlationId,
        string tipo) => new()
        {
            ["PK"] = new($"INBOX#{messageId}"),
            ["SK"] = new("MESSAGE"),
            ["EntityType"] = new("Inbox"),
            ["MessageId"] = new(messageId.ToString()),
            ["CorrelationId"] = new(correlationId.ToString()),
            ["Tipo"] = new(tipo),
            ["ProcessadoEm"] = new(DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture))
        };

    public static Dictionary<string, AttributeValue> CriarOutbox(
        Guid messageId,
        Guid correlationId,
        string tipo,
        string payload,
        DateTimeOffset criadoEm) => new()
        {
            ["PK"] = new($"OUTBOX#{messageId}"),
            ["SK"] = new("MESSAGE"),
            ["EntityType"] = new("Outbox"),
            ["MessageId"] = new(messageId.ToString()),
            ["CorrelationId"] = new(correlationId.ToString()),
            ["Tipo"] = new(tipo),
            ["Destino"] = new("OficinaEventos"),
            ["Payload"] = new(payload),
            ["CriadoEm"] = new(criadoEm.ToString("O", CultureInfo.InvariantCulture)),
            ["Tentativas"] = new() { N = "0" }
        };
}
