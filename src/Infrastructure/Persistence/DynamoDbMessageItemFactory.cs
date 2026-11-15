using System.Globalization;
using Amazon.DynamoDBv2.Model;
using Fiap.TechChallenge.OficinaMecanica.Billing.Infrastructure.Observability;

namespace Fiap.TechChallenge.OficinaMecanica.Billing.Infrastructure.Persistence;

internal static class DynamoDbMessageItemFactory
{
    /// <summary>
    /// Atributo com o contexto do trace do Datadog de quem gravou o item (F4-31).
    /// Na Outbox, a publicacao continua esse trace; na Inbox, guarda o trace da
    /// Saga para as etapas humanas (aprovacao do orcamento, webhook do Mercado
    /// Pago) publicarem no mesmo trace.
    /// </summary>
    public const string TraceContext = "TraceContext";

    public static Dictionary<string, AttributeValue> CriarInbox(
        Guid messageId,
        Guid correlationId,
        string tipo) => ComTraceContext(new()
        {
            ["PK"] = new($"INBOX#{messageId}"),
            ["SK"] = new("MESSAGE"),
            ["EntityType"] = new("Inbox"),
            ["MessageId"] = new(messageId.ToString()),
            ["CorrelationId"] = new(correlationId.ToString()),
            ["Tipo"] = new(tipo),
            ["ProcessadoEm"] = new(DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture))
        });

    public static Dictionary<string, AttributeValue> CriarOutbox(
        Guid messageId,
        Guid correlationId,
        string tipo,
        string payload,
        DateTimeOffset criadoEm) => ComTraceContext(new()
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
        });

    private static Dictionary<string, AttributeValue> ComTraceContext(Dictionary<string, AttributeValue> item)
    {
        if (MensageriaTrace.CapturarContextoAtual() is { } contexto)
            item[TraceContext] = new(contexto);
        return item;
    }
}
