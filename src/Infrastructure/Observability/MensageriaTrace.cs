using System.Text;
using System.Text.Json;
using Datadog.Trace;

namespace Fiap.TechChallenge.OficinaMecanica.Billing.Infrastructure.Observability;

/// <summary>
/// Propaga o contexto de trace do Datadog pela mensageria, para o fluxo de uma
/// OS aparecer como um unico trace no APM (F4-31).
/// <para>
/// A Outbox quebra o trace: a mensagem e gravada dentro da requisicao (ou do
/// consumo) que a gerou, mas publicada depois por um worker sem span ativo.
/// Por isso o contexto do span ativo e gravado junto da mensagem
/// (<see cref="CapturarContextoAtual"/>), reaberto como pai do span de
/// publicacao (<see cref="IniciarPublicacao"/>) e enviado no atributo
/// <c>_datadog</c> da mensagem, o mesmo que o tracer .NET usa na
/// instrumentacao automatica do AWSSDK. O consumidor extrai o atributo e
/// processa a mensagem num span filho (<see cref="IniciarConsumo"/>).
/// </para>
/// <para>
/// O contexto e o JSON dos headers que o <see cref="SpanContextInjector"/>
/// escreve (<c>x-datadog-trace-id</c>, <c>x-datadog-parent-id</c>,
/// <c>x-datadog-sampling-priority</c>, <c>x-datadog-tags</c>,
/// <c>traceparent</c>, <c>tracestate</c>), conforme
/// <c>DD_TRACE_PROPAGATION_STYLE</c>. Sem o tracer automatico carregado
/// (testes, execucao local sem profiler) a API do Datadog.Trace nao faz nada e
/// os metodos devolvem <c>null</c>.
/// </para>
/// </summary>
public static class MensageriaTrace
{
    /// <summary>Atributo da mensagem SNS/SQS com o contexto do trace.</summary>
    public const string AtributoMensagem = "_datadog";

    /// <summary>Valores de <c>messaging.system</c> (convencao do OpenTelemetry).</summary>
    public const string SistemaSqs = "aws_sqs";
    public const string SistemaSns = "aws_sns";

    private const string TraceIdHeader = "x-datadog-trace-id";
    private static readonly SpanContextInjector Injector = new();
    private static readonly SpanContextExtractor Extractor = new();

    /// <summary>Contexto do span ativo serializado, ou <c>null</c> sem span ativo.</summary>
    public static string? CapturarContextoAtual()
    {
        var span = Tracer.Instance.ActiveScope?.Span;
        if (span is null)
            return null;

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        Injector.Inject(headers, static (carrier, chave, valor) => carrier[chave] = valor, span.Context);
        return headers.Count == 0 ? null : JsonSerializer.Serialize(headers);
    }

    /// <summary>
    /// Escolhe o pai do span de publicacao. Uma mensagem gerada por uma etapa
    /// humana (requisicao HTTP de um mecanico, aprovacao do cliente, webhook do
    /// Mercado Pago) nasce num trace novo; se o servico ja consumiu mensagens da
    /// mesma Saga, a publicacao continua o trace da Saga e guarda o trace da
    /// requisicao em <see cref="ContextoPublicacao.TraceIdOrigem"/>.
    /// </summary>
    public static ContextoPublicacao EscolherContextoPai(string? contextoDaMensagem, string? contextoDaSaga)
    {
        var traceDaMensagem = LerTraceId(contextoDaMensagem);
        var traceDaSaga = LerTraceId(contextoDaSaga);

        if (traceDaSaga is null || traceDaMensagem == traceDaSaga)
            return new ContextoPublicacao(contextoDaMensagem, null);
        if (traceDaMensagem is null)
            return new ContextoPublicacao(contextoDaSaga, null);
        return new ContextoPublicacao(contextoDaSaga, traceDaMensagem);
    }

    /// <summary>
    /// Abre o span de publicacao de uma mensagem da Outbox como filho do
    /// contexto gravado. Dentro dele, <see cref="CapturarContextoAtual"/> devolve
    /// o contexto que vai no atributo <see cref="AtributoMensagem"/>.
    /// </summary>
    public static IScope IniciarPublicacao(
        ContextoPublicacao contexto,
        string tipoMensagem,
        string sistema,
        string destino,
        Guid correlationId,
        int? ordemServicoId)
    {
        var scope = Iniciar("mensageria.publicar", contexto.Pai, tipoMensagem, "producer", sistema, destino, correlationId, ordemServicoId);
        if (contexto.TraceIdOrigem is not null)
            scope.Span.SetTag("oficina.trace_origem", contexto.TraceIdOrigem);
        return scope;
    }

    /// <summary>
    /// Abre o span de processamento de uma mensagem consumida, filho do
    /// contexto recebido no atributo <see cref="AtributoMensagem"/>. O que for
    /// gravado na Outbox durante o processamento herda este trace.
    /// </summary>
    public static IScope IniciarConsumo(string? contextoRecebido, string tipoMensagem, string queueUrl) =>
        Iniciar("mensageria.consumir", contextoRecebido, tipoMensagem, "consumer", SistemaSqs, NomeDaFila(queueUrl), null, null);

    /// <summary>Marca o span com a Saga e a OS, para achar o trace pela OS no APM.</summary>
    public static void IdentificarMensagem(IScope scope, Guid? correlationId, int? ordemServicoId)
    {
        if (correlationId is not null)
            scope.Span.SetTag("oficina.correlation_id", correlationId.Value.ToString());
        if (ordemServicoId is not null)
            scope.Span.SetTag("oficina.ordem_servico_id", ordemServicoId.Value.ToString());
    }

    internal static string NomeDaFila(string queueUrl)
    {
        var barra = queueUrl.TrimEnd('/').LastIndexOf('/');
        return barra < 0 ? queueUrl : queueUrl[(barra + 1)..];
    }

    /// <summary>
    /// Le o atributo <see cref="AtributoMensagem"/> de uma mensagem SQS. Com
    /// raw message delivery o atributo publicado no SNS chega igual na fila; o
    /// tracer automatico grava como Binary no SNS e como String no SQS.
    /// </summary>
    public static string? LerAtributo(IDictionary<string, Amazon.SQS.Model.MessageAttributeValue>? atributos)
    {
        if (atributos is null || !atributos.TryGetValue(AtributoMensagem, out var atributo))
            return null;
        if (!string.IsNullOrWhiteSpace(atributo.StringValue))
            return atributo.StringValue;
        return atributo.BinaryValue is { Length: > 0 } binario
            ? Encoding.UTF8.GetString(binario.ToArray())
            : null;
    }

    internal static Dictionary<string, string>? Desserializar(string? contexto)
    {
        if (string.IsNullOrWhiteSpace(contexto))
            return null;

        try
        {
            var headers = JsonSerializer.Deserialize<Dictionary<string, string>>(contexto);
            return headers is null
                ? null
                : new Dictionary<string, string>(headers, StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    internal static string? LerTraceId(string? contexto) =>
        Desserializar(contexto)?.GetValueOrDefault(TraceIdHeader);

    private static ISpanContext? Extrair(string? contexto)
    {
        var headers = Desserializar(contexto);
        return headers is null
            ? null
            : Extractor.Extract(
                headers,
                static (carrier, chave) => carrier.TryGetValue(chave, out var valor) ? [valor] : []);
    }

    private static IScope Iniciar(
        string operacao,
        string? contextoPai,
        string tipoMensagem,
        string spanKind,
        string sistema,
        string destino,
        Guid? correlationId,
        int? ordemServicoId)
    {
        var scope = Tracer.Instance.StartActive(operacao, new SpanCreationSettings { Parent = Extrair(contextoPai) });
        var span = scope.Span;
        span.ResourceName = tipoMensagem;
        span.Type = "queue";
        span.SetTag("span.kind", spanKind);
        span.SetTag("messaging.system", sistema);
        span.SetTag("messaging.destination.name", destino);
        span.SetTag("messaging.message.type", tipoMensagem);
        IdentificarMensagem(scope, correlationId, ordemServicoId);
        return scope;
    }
}

/// <summary>Contexto pai de uma publicacao e, se veio de outro trace, o id dele.</summary>
public sealed record ContextoPublicacao(string? Pai, string? TraceIdOrigem);
