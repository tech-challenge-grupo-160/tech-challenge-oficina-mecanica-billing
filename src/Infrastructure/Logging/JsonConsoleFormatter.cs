using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Fiap.TechChallenge.OficinaMecanica.Billing.Shared.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;

namespace Fiap.TechChallenge.OficinaMecanica.Billing.Infrastructure.Logging;

/// <summary>
/// Log em JSON, uma linha por evento (F4-32).
///
/// Campos fixos: <c>correlationId</c> e <c>ordemServicoId</c> (do escopo aberto
/// por <see cref="LogContexto"/> ou do proprio log), <c>trace_id</c> e
/// <c>span_id</c>. O bloco <c>dd</c> segue os atributos reservados do Datadog
/// (<c>dd.trace_id</c>, <c>dd.span_id</c>, <c>dd.service</c>...), que ligam o log
/// ao trace. `level`, `message` e `timestamp` tambem seguem os nomes que o
/// Datadog reconhece sozinho (o status HTTP fica em `statusHttp` para nao ser
/// lido como nivel do log). Com <c>DD_LOGS_INJECTION=true</c> o tracer do Datadog coloca os
/// ids dele no escopo do log; sem o tracer (local e testes) vale o
/// <see cref="Activity"/> corrente, convertido para o formato do Datadog.
/// </summary>
public sealed class JsonConsoleFormatter : ConsoleFormatter, IDisposable
{
    public const string FormatterName = "structured-json";

    private static readonly HashSet<string> CamposReservados = new(StringComparer.Ordinal)
    {
        "{OriginalFormat}", "rota", "statusHttp", "duracao", "traceId",
        LogContexto.CorrelationId, "CorrelationId", LogContexto.OrdemServicoId, "OrdemServicoId"
    };

    private readonly IDisposable? _optionsReloadToken;
    private JsonConsoleFormatterOptions _options;

    public JsonConsoleFormatter(IOptionsMonitor<JsonConsoleFormatterOptions> options)
        : base(FormatterName)
    {
        _options = options.CurrentValue;
        _optionsReloadToken = options.OnChange(updated => _options = updated);
    }

    public override void Write<TState>(
        in LogEntry<TState> logEntry,
        IExternalScopeProvider? scopeProvider,
        TextWriter textWriter)
    {
        var state = ReadState(logEntry.State);
        var scope = ReadScopes(scopeProvider);
        var renderedMessage = logEntry.Formatter?.Invoke(logEntry.State, logEntry.Exception) ?? string.Empty;
        var trace = ResolverTrace(scope);

        var log = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["timestamp"] = (_options.UseUtcTimestamp ? DateTimeOffset.UtcNow : DateTimeOffset.Now).ToString("O"),
            ["level"] = ToLevelLabel(logEntry.LogLevel),
            ["nivel"] = ToLevelLabel(logEntry.LogLevel),
            ["message"] = SensitiveDataMasker.Mask(renderedMessage),
            ["mensagem"] = SensitiveDataMasker.Mask(renderedMessage),
            ["servico"] = _options.ServiceName,
            ["ambiente"] = _options.EnvironmentName,
            ["categoria"] = logEntry.Category,
            ["correlationId"] = Primeiro(state, scope, LogContexto.CorrelationId, "CorrelationId"),
            ["ordemServicoId"] = Primeiro(state, scope, LogContexto.OrdemServicoId, "OrdemServicoId"),
            ["trace_id"] = trace?.TraceId,
            ["span_id"] = trace?.SpanId,
            ["rota"] = GetValue(state, "rota"),
            ["statusHttp"] = GetValue(state, "statusHttp"),
            ["duracao"] = GetValue(state, "duracao")
        };

        foreach (var property in scope)
        {
            if (!CamposReservados.Contains(property.Key) && LogContexto.Campos.Contains(property.Key))
                log[property.Key] = MaskValue(property.Value);
        }

        foreach (var property in state)
        {
            if (!CamposReservados.Contains(property.Key))
                log[property.Key] = MaskValue(property.Value);
        }

        if (trace is not null)
        {
            log["dd"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["trace_id"] = trace.DatadogTraceId,
                ["span_id"] = trace.DatadogSpanId,
                ["service"] = Texto(scope, "dd_service") ?? Environment.GetEnvironmentVariable("DD_SERVICE") ?? _options.ServiceName,
                ["env"] = Texto(scope, "dd_env") ?? Environment.GetEnvironmentVariable("DD_ENV"),
                ["version"] = Texto(scope, "dd_version") ?? Environment.GetEnvironmentVariable("DD_VERSION")
            };
        }

        if (logEntry.Exception is not null)
        {
            log["erro"] = SensitiveDataMasker.Mask(logEntry.Exception.ToString());
        }

        textWriter.WriteLine(JsonSerializer.Serialize(log));
    }

    public void Dispose() => _optionsReloadToken?.Dispose();

    internal sealed record TraceLog(string TraceId, string SpanId, string DatadogTraceId, string DatadogSpanId);

    /// <summary>
    /// Ids do tracer do Datadog quando ele injetou o escopo; senao, o
    /// <see cref="Activity"/> corrente. O Datadog correlaciona pelos 64 bits
    /// inferiores do trace W3C, em decimal.
    /// </summary>
    internal static TraceLog? ResolverTrace(IReadOnlyDictionary<string, object?> scope)
    {
        var ddTraceId = Texto(scope, "dd_trace_id");
        var ddSpanId = Texto(scope, "dd_span_id");
        if (!string.IsNullOrWhiteSpace(ddTraceId) && !string.IsNullOrWhiteSpace(ddSpanId))
            return new TraceLog(ddTraceId, ddSpanId, ddTraceId, ddSpanId);

        var activity = Activity.Current;
        if (activity is null || activity.TraceId == default)
            return null;

        var traceId = activity.TraceId.ToHexString();
        var spanId = activity.SpanId.ToHexString();
        return new TraceLog(
            traceId,
            spanId,
            ParaDecimal(traceId[^16..]),
            ParaDecimal(spanId));
    }

    private static string ParaDecimal(string hex) =>
        ulong.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture)
            .ToString(CultureInfo.InvariantCulture);

    private static Dictionary<string, object?> ReadState<TState>(TState state)
    {
        var values = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (state is IEnumerable<KeyValuePair<string, object?>> properties)
        {
            foreach (var property in properties)
            {
                values[property.Key] = property.Value;
            }
        }

        return values;
    }

    /// <summary>
    /// Junta os escopos ativos; o mais interno vence. Escopos que nao sao
    /// pares chave/valor (ex.: texto livre) sao ignorados.
    /// </summary>
    private static Dictionary<string, object?> ReadScopes(IExternalScopeProvider? scopeProvider)
    {
        var values = new Dictionary<string, object?>(StringComparer.Ordinal);
        scopeProvider?.ForEachScope(static (scope, acumulado) =>
        {
            if (scope is IEnumerable<KeyValuePair<string, object?>> properties)
            {
                foreach (var property in properties)
                {
                    acumulado[property.Key] = property.Value;
                }
            }
        }, values);
        return values;
    }

    private static object? Primeiro(
        IReadOnlyDictionary<string, object?> state,
        IReadOnlyDictionary<string, object?> scope,
        params string[] chaves)
    {
        foreach (var origem in new[] { state, scope })
        {
            foreach (var chave in chaves)
            {
                if (origem.TryGetValue(chave, out var valor) && valor is not null)
                    return MaskValue(valor);
            }
        }

        return null;
    }

    private static string? Texto(IReadOnlyDictionary<string, object?> values, string key) =>
        values.TryGetValue(key, out var value) ? value?.ToString() : null;

    private static object? GetValue(IReadOnlyDictionary<string, object?> state, string key)
    {
        return state.TryGetValue(key, out var value) ? MaskValue(value) : null;
    }

    private static object? MaskValue(object? value)
    {
        return value switch
        {
            null => null,
            string text => SensitiveDataMasker.Mask(text),
            byte or sbyte or short or ushort or int or uint or long or ulong
                or float or double or decimal or bool or char
                or DateTime or DateTimeOffset or TimeSpan or Guid => value,
            _ => "[REDACTED]"
        };
    }

    private static string ToLevelLabel(LogLevel logLevel)
    {
        return logLevel switch
        {
            LogLevel.Trace => "trace",
            LogLevel.Debug => "debug",
            LogLevel.Information => "information",
            LogLevel.Warning => "warning",
            LogLevel.Error => "error",
            LogLevel.Critical => "critical",
            _ => "none"
        };
    }
}
